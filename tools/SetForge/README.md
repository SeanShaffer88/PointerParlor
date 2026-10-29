# SetForge

Draws PointerParlor's 12 signature cursor sets into `sets/` and a preview sheet per set into `docs/sets/`.

```bash
dotnet run -c Release --project tools/SetForge
```

Draw a single set:

```bash
dotnet run -c Release --project tools/SetForge -- --only neon
```

- `Shapes.cs`: the 17 pointer designs on a 32×32 grid, in two families (Solid and Line).
- `Themes.cs`: one entry per set: colors, outlines, glow, and effects (gloss, scanlines, grid, pixelation, rough ink, splatter).
- `Renderer.cs`: draws a design in a theme at a pixel size.
- `Program.cs`: renders 32, 48, 64, 96 and 128 px images, writes `.cur`/`.ani` files and `set.json`.

## Openly licensed sets

`--open` converts openly licensed Linux cursor themes into Windows sets (catalog in `OpenSets.cs`):

```bash
tools/SetForge/clone-open-sources.sh ../open-sources
```

```bash
dotnet run -c Release --project tools/SetForge -- --open ../open-sources
```

- **Xcursor themes** (`XcursorThemeSource`): built Linux themes. Linux nominal 24 px maps to Windows 32 px.
- **SVG packs** (`SvgPackSource`): Bibata, Fuchsia, Banana and XCursor-Pro, rendered from their SVGs with each
  variant's colors, using the authors' own hotspots and Windows role choices, at their "Large" Windows scale.
- **SVG + spec themes** (`SvgSpecSource`): Capitaine and Volantes, 24-unit SVGs with per-cursor hotspot specs.
- Linux names map to Windows roles through `LinuxNames`; themes without Location or Person pointers reuse
  Normal Select. Each set gets the project's license as `LICENSE.txt` and credits in `set.json`.

When you change a set, bump `SetVersion` in `Program.cs` so installed copies refresh, then run the
Core tests: `BundledSetTests` checks every set is complete and readable.
