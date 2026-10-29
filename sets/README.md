# Bundled cursor sets

Each subfolder here is one set that ships with PointerParlor: a `set.json` plus its cursor files.
On first run (and after an update that bumps a set's `version`) the app copies these folders into
`%LocalAppData%\PointerParlor\Library`, because Windows reads cursor files from the paths stored in
the registry and those must not move when the app is updated.

`set.json` looks like this:

```json
{
  "id": "lagoon",
  "name": "Lagoon",
  "kind": "Signature",
  "author": "PointerParlor",
  "license": "MIT",
  "version": 1,
  "cursors": { "Arrow": "Arrow.cur", "Wait": "Wait.ani" }
}
```

`kind` is `Signature` for PointerParlor's own sets or `OpenLicense` for openly licensed packs, which
must also set `license` and `sourceUrl` and be credited in `THIRD-PARTY-NOTICES.md`. Cursor keys are
the Windows registry names: Arrow, Help, AppStarting, Wait, Crosshair, IBeam, NWPen, No, SizeNS,
SizeWE, SizeNWSE, SizeNESW, SizeAll, UpArrow, Hand, Pin, Person.

The 12 signature sets (velvet, lagoon, brass, ink, chalk, pixel, glass, candy, blueprint, phosphor,
neon, beacon-xl) are drawn by `tools/SetForge`; do not edit their files by hand, change the generator
and run it again. Preview sheets are in `docs/sets/`. Openly licensed packs are not bundled yet.
