# PointerParlor

A gallery of mouse cursors for Windows. Browse the built-in cursor sets, preview every pointer in a set, and apply a single cursor or the whole set in one click. Import your own sets or individual cursor files to add them to the gallery.

> **Status:** early planning. Nothing is ready to download yet. The features below describe what PointerParlor is being built to do.

## Features

- **Built-in gallery.** PointerParlor ships with a collection of cursor sets, each shown as a thumbnail card.
- **Set detail view.** Open a set to see each of its pointers (Normal Select, Text Select, Busy, Working in Background, Link Select, Resize, Move, and the rest) with its name and an animated preview.
- **Apply one cursor or the whole set.** Swap only the pointer you want, or apply every cursor in a set at once.
- **Mix and match.** Build your own combination from cursors in different sets.
- **Import.** Add a whole set from an `.inf` install package, a folder or a `.zip`. Add single `.cur` or `.ani` files.
- **Reset to default.** Go back to the standard Windows cursors in one click.
- **No admin rights needed.** Cursors are applied to your own user account only.

## How it works

Windows stores the current user's pointer scheme under `HKEY_CURRENT_USER\Control Panel\Cursors`, one value for each pointer role. PointerParlor writes the chosen cursor files to those values and tells Windows to reload them, so changes take effect right away without signing out.

## Supported formats

| Format | Use |
| --- | --- |
| `.cur` | Static cursor |
| `.ani` | Animated cursor |
| `.inf` | Cursor set install package |
| `.zip` / folder | Cursor set as a bundle of files |

## Requirements

- Windows 10 or Windows 11

## Roadmap

1. Gallery of built-in sets with thumbnails
2. Set detail view with per-pointer previews
3. Apply a single cursor, apply a full set, reset to default
4. Import sets and single cursors
5. Custom mixed sets

## License

[MIT](LICENSE)
