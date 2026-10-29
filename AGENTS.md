# AGENTS.md

Notes for agents working on this repo.

- Layout: `src/PointerParlor.Core` holds all testable logic (plain .NET 10, no Windows APIs). `src/PointerParlor` is the WinUI 3 shell (unpackaged, self-contained). `tests/PointerParlor.Core.Tests` is the xUnit suite. Keep new logic in Core with tests unless it truly needs Windows.
- Only `Services/RegistryApplier.cs` in the shell may change Windows. Everything else plans changes through `SchemePlanner` and records an `ApplySnapshot` first, so every change can be undone.
- Gate before publishing: the FULL Core suite (`dotnet test tests/PointerParlor.Core.Tests -c Release`, never a filtered subset) and a Release publish of the app on Windows with the `--smoke-test` launch passing. CI runs both for x64 and ARM64.
- The XAML compiler only runs on Windows. On Linux you can build and test Core; the shell is verified by CI.
- Stack decision (WinUI 3 / .NET instead of Rust or WPF) is recorded in `DECISIONS.md`. Do not change it without revisiting that entry with the maintainer.
- Keep `DECISIONS.md` and `KNOWN_LIMITATIONS.md` current as behavior changes. Label runtime behavior honestly: Verified, Partially verified, or Unverified.
- Dependencies are pinned in the csproj files. Bump deliberately.
- Bundled cursor sets go in `sets/` (format in `sets/README.md`). Every openly licensed pack must be credited in `THIRD-PARTY-NOTICES.md`.
- Standing defaults: MIT license, Windows 10 and 11 support, no telemetry, no network access, no winget (binaries are unsigned).
