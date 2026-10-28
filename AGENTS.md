# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
the downloads; this file covers how the code is put together and the rules that keep it working.

## What this is

A monster-fighting career in the style of Electronic Arts' 1985 game Mail Order Monsters, in C# on .NET 10 and
Avalonia 12. One self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). The graphics
are drawn in code, and the sounds and tunes are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVAMailOrderMonsters.slnx            # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAMailOrderMonsters.slnx             # every test runs headless: no window opens
dotnet run --project src/AVAMailOrderMonsters     # play it; --no-audio for silence
MONSTER_SHOTS=$PWD/Images dotnet test tests/AVAMailOrderMonsters.Tests --filter ScreenshotTests   # refresh the README's pictures
MONSTER_PROBE=1 dotnet test tests/AVAMailOrderMonsters.Tests --filter PilotProbe   # report ten computer careers (MONSTER_PROBE=lazy: a player who does nothing)
python3 tools/make_sounds.py                      # rebuild src/AVAMailOrderMonsters/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `net10.0`, `TreatWarningsAsErrors`, nullable reference types and implicit usings
for every project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVAMailOrderMonsters/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, attract, shop, play, initials, high scores), the career and the
    fixed-step loop.
  - `Catalog.cs` has `Controls`, `Field` (the screen and arena sizes, the step length), the species, the
    weapons and gear, and the leagues.
  - `Arena.cs` has the `Dice` generator and a bout's state: monsters, props, shots and the clock.
  - `Sim.cs` builds an arena and steps a bout. `Brain.cs` handles the computer's monster, and the player's
    in the attract mode and the tests.
  - `InputHandler.cs` (keys) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVAMailOrderMonsters/Views/`: `GameCanvas` draws the shop and the arena (`GameCanvas.Draw.cs`) and runs
  the timer.
- `src/AVAMailOrderMonsters/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `src/AVAMailOrderMonsters/Assets/Fonts/`: the VT323 font, under the OFL.
- `tests/AVAMailOrderMonsters.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: gameplay, whole careers
  (`CareerTests.cs`), screenshots, the Shift release case and the `PilotProbe` tuning report.

## Rules that keep it working

- **Fixed steps.** `GameEngine` collects frame time (at most 0.25 s a frame) and advances in fixed 1/60 s steps
  (`Field.Step`), so tests can play whole bouts and careers deterministically from a seed. Draw random numbers
  from a seeded `Dice` (the engine's, the bout's or the brain's), never from `Random` or `Random.Shared`. A
  `Dice` is a struct whose state is copied with the bout, so take it, use it and store it back as `Sim` does.
- **Engine first, drawing second.** Gameplay belongs in `Engine/`, where tests can reach it without a window.
  `Views/` only reads the game and draws it.
- **Input goes through `InputHandler`.** `Controls.Dy` is -1 for up the screen. Keys, D-pad and stick must all
  keep that sign. Test new controls through `InputHandler`, not by building `Controls` directly.
  - Keys are tracked by `PhysicalKey`, because on some Linux setups Avalonia reports the release of Shift as
    CapsLock. Shift changes weapons, and `ShiftReleaseTests` covers this.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up; a player's own `ALSOFT_CONF` is left alone.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVAMailOrderMonsters`);
  tests load the table from a fresh temp folder and set `Silent = true` on the engine.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- Prices, damage and league budgets are balanced together: run `CareerTests` (and the probe) after changing
  any of them.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the catalogue) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request. It then publishes self-contained
single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. A release also asks the portfolio site to rebuild.
