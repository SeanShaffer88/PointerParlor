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

When you change a set, bump `SetVersion` in `Program.cs` so installed copies refresh, then run the
Core tests: `BundledSetTests` checks every set is complete and readable.
