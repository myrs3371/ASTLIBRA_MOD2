# ASTLIBRA QuickSave reverse-engineering log

## Confirmed

- Target: `ASTLIBRA.exe`, version `V1.5.0`, x64 native executable.
- Save directory: `C:\Users\lenovo\AppData\Local\ASTLIBRA\SAVE`.
- Save header: `ALBR_SAV`.
- Relevant embedded data strings include `ALBR_SAV`, `CRC Error 1`, `CRC Error 2`, `ALBR_RET`, `NO DATA`, and `Sound\\SE\\System\\SAVE.wav`.
- Static analysis identified the executable sections with image base `0x140000000`; `.text` begins at `0x140001000`, `.rdata` at `0x140877000`.
- Simulated menu navigation did not change any `SAVE_*.DAT` timestamp or contents, so the feature must reach the game's internal save path rather than press a guessed menu sequence.

## Runtime trace build

`AstlibraQuickSave.dll` currently patches the game's import table for `CreateFileW` and `WriteFile` after process startup. It only records file paths and writes associated with a `SAVE_*` handle; it does not alter save data.

The trace is written to `E:\game\ASTLIBRA\ASTLIBRA\AstlibraQuickSave.log`. A manual save at a save point is required to capture the caller address for the real serialization path.

## Multiple save points

- Two manual saves from different save points both wrote `SAVE_0002.DAT` using the same low-level writer chain (`RVA 0x84F187`, with common callers around `0x843B80` and `0x843921`).
- The file-open path differs above the common writer; the second trace includes `RVA 0x857AEC`, `0x8572FC`, and `0x857DFD`.
- This indicates that save-point-specific state/permission checks occur above a shared serialization layer. The eventual quick-save hook must preserve the current slot and current world state rather than hard-code a save-point-specific path.

## Safety

- The existing `AstlibraMod` project is not modified.
- No game executable patch and no save-file modification has been made.
- Before any save-related write test, preserve the user's current save files.
