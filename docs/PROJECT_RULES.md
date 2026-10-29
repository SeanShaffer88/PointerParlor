# PointerParlor: Project Rules

Purpose in one line: a gallery of mouse cursor sets for Windows, with per-pointer and whole-set apply, import, mixes and undo.
Frozen foundation: `src/PointerParlor.Core` = all testable logic (plain .NET 10, no Windows APIs); `src/PointerParlor` = thin WinUI 3 shell. Registry writes go only through `SchemePlanner` → `ApplySnapshot` → `RegistryApplier`.
Build: `dotnet test tests/PointerParlor.Core.Tests -c Release` (any OS) · `dotnet publish src/PointerParlor -c Release -r win-x64` (Windows only) · Full gate: both, plus `PointerParlor.exe --smoke-test` exiting 0.
Coverage floors: Core logic is the coverage surface; new Core features ship with tests in the same change.
Special rules: never write outside HKCU; never move a file Windows may be pointing at; snapshot before every registry change; no network access; no telemetry; no winget; openly licensed sets must be credited in THIRD-PARTY-NOTICES.md.
Known limitations live at: `KNOWN_LIMITATIONS.md` · Decision log: `DECISIONS.md`
