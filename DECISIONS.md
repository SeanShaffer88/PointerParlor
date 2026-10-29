# Decisions

A living log of significant architecture, UX, and dependency decisions.

## 2026-10-29: PointerParlor v0.1 foundation

- **Product.** A gallery of mouse cursor sets for Windows: browse sets, preview every pointer, apply one cursor or a whole set, import sets and single cursors, save mixes, change pointer size, undo the last change, reset to the Windows default.
- **Stack: C# / WinUI 3 on .NET 10, unpackaged and self-contained.** This deviates from the Rust default. The reason is a native Windows 11 look (Mica, Fluent controls, a virtualized grouped grid for the gallery) with first-party tooling. WPF, used by ArenaDesktop, was passed over for the same reason. Unpackaged rather than MSIX because a packaged app can have its registry and AppData writes redirected into a private copy, and cursor paths must be visible to Windows itself.
- **Core / shell split** (as in ArenaDesktop). Core has no Windows APIs: cursor decoding, the library, import, and *planning* registry changes. The shell's `RegistryApplier` is the only code that writes the registry and calls `SystemParametersInfo(SPI_SETCURSORS)`.
- **Library lives in `%LocalAppData%\PointerParlor\Library`.** Windows reads cursor files from the paths stored in `HKCU\Control Panel\Cursors`, so applied files must never move. Bundled sets are copied there on first run and refreshed only when a set's `version` increases. Imported files are copied in, so moving or deleting the originals breaks nothing. Copied files are renamed to their role (`Arrow.cur`), which keeps paths free of commas, as scheme strings require.
- **Applying a set registers a Windows scheme** named `PointerParlor - <set>` under `HKCU\Control Panel\Cursors\Schemes`, so it also shows in Mouse Properties. Windows' own schemes are applied as system schemes and are read from HKLM, not copied.
- **Undo** keeps one snapshot (`undo.json`) of the exact values the last change overwrote, including values that did not exist. This borrows RegKit's undo-manifest idea without shipping the native DLL.
- **Settings** never store defaults and carry a schema version (the SettingsKit rule), implemented in C#.
- **Pointer size** writes `CursorBaseSize` (32–256 px in 16 px steps, matching the Windows slider), then reloads cursors.
- **Native DLL kits not used.** RegKit, SettingsKit, ImageKit, ArchiveKit and others would mean shipping x64 and ARM64 native builds plus bindings for jobs .NET already does; their ideas are adopted instead.
- **Look.** The Fanned Set icon's palette (teal accent, coral and gold highlights) over Mica, in light and dark themes, instead of the black/red family look. Chosen by the maintainer.
- **Bundled content is a separate piece of work.** The 12 signature sets (Velvet, Lagoon, Brass, Ink, Chalk, Pixel, Glass, Candy, Blueprint, Phosphor, Neon, Beacon XL) and openly licensed packs come later; v0.1 shows the schemes included with Windows plus imports.
- **Distribution.** GitHub Releases only, unsigned, stated honestly. No winget.

## 2026-10-29: Include the application resource index when publishing

The unpackaged publish output omitted `PointerParlor.pri`, although the build output contained it. The resulting app failed to load `MainWindow` with a `XamlParseException`. Adding that exact generated PRI to the original published output made the same executable pass `--smoke-test`, confirming the packaging fault. The project now includes the PRI in the publish file list and fails publishing if it is missing. No control, resource, dependency, or application behavior was changed.

Verified locally on Windows x64: the full Core suite passes 101 tests, fresh Release publishes for x64 and ARM64 contain the application PRI, and the fresh x64 publish passes `--smoke-test`. ARM64 execution is left to its native CI runner; interactive cursor application remains unverified.

## 2026-10-29: Signature sets are generated, not hand-drawn

- **`tools/SetForge` draws all 12 sets** from shared vector geometry (`Shapes.cs`) and per-set styles (`Themes.cs`), using SkiaSharp. One shape change updates every set; one style tweak updates all 17 pointers of a set. The output (`sets/<id>/`) is committed, so building the app never needs the tool.
- **Two shape families.** *Solid*: filled shapes with outlines (Velvet, Lagoon, Brass, Ink, Pixel, Candy, Blueprint, Phosphor, Beacon XL). *Line*: rounded outlines with chevron and corner-bracket resize cursors, colored badges and a gradient spinner (Glass, Chalk, Neon). The Line family takes its *style* from the maintainer's reference, the Outlier cursor set by anoshione; no Outlier artwork is copied.
- **Ink and Neon** take cues from the maintainer's graffiti-eye reference: rough ink edges with crimson, cyan and orange paint splatter (Ink), and a glitching cyan/pink color split with orange accents (Neon).
- **Sizes 32, 48, 64, 96, 128 px** in every file, so sets stay sharp at larger pointer sizes. Images of 64 px and up are stored PNG-compressed, which keeps the 12 sets near 11 MB instead of about 45 MB; 32 and 48 px stay as classic bitmaps that every Windows version reads.
- **Busy and Working in Background** are 10-frame animations at 4/60 s per frame.
- **Signature sets are MIT licensed** with the app, author "PointerParlor".

## 2026-10-29: Persist undo before changing registry or library files

All apply, rename, and delete operations now atomically persist the complete undo journal before their first mutation. A persistence failure refuses the operation. The in-memory undo state is published before registry writes so a partly failed apply remains recoverable. Rename includes its original manifest; delete moves the set into a private library-local `.undo` directory instead of destroying files still needed for undo. Undo restores those files before restoring registry paths. A failed mutation restores the library files and retains the journal for registry recovery; the next journaled operation expires the previous single-level undo copy.

Restoration and cleanup accept only immediate children of the owned library/staging roots and reject symbolic links or junctions. A conflicting restored directory is never overwritten. Core regression tests cover durable-before-mutation ordering, persistence failure, rename/delete restoration after journal reload, staging rollback, path escape refusal, and expiration of the previous staged set. No UI, dependency, or registry access boundary changed; `RegistryApplier` remains the only registry writer.

The full Core suite passes 111 tests on Windows, including old snapshot compatibility, occupied-directory preservation, and actual junction/ancestor refusal. The x64 Release publish passes `--smoke-test`; ARM64 publishes successfully, with native execution left to CI.
