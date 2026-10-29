# Known limitations

Status labels: **Verified** (checked on a real Windows machine), **Partially verified** (CI or tests only), **Unverified**.

- **The app has not been run interactively on Windows yet** (Unverified). CI builds it, runs the Core suite, and launches it with `--smoke-test` (loads services and builds the window, changes nothing).
- **No bundled sets yet.** The gallery shows the schemes included with Windows plus imported sets. The signature and openly licensed sets are a separate piece of work.
- **Pointer size changes** write `CursorBaseSize` and reload cursors. This matches what the Windows size slider stores, but whether every Windows build picks the new size up without signing out is Unverified.
- **Screen-inverting pixels** (used by some classic monochrome cursors) are previewed as dark translucent pixels; Windows draws them by inverting the screen.
- **Compressed bitmap data** (RLE) inside cursor files is not supported; such files are skipped at import with a message. PNG-compressed images are supported.
- **Undo is one level deep**: it reverts the most recent change only.
- **Deleting a set in use is refused**, because Windows reads its files directly. Apply another set or reset to default first.
- **Renaming a set** re-registers its Windows scheme under the new name; the old scheme entry is removed.
