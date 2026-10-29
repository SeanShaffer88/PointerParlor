# Known limitations

Status labels: **Verified** (checked on a real Windows machine), **Partially verified** (CI or tests only), **Unverified**.

- **The app has not been run interactively on Windows yet** (Unverified). The full Core suite (111 tests), Release publishes for x64 and ARM64, and the x64 `--smoke-test` launch were verified locally on Windows. The publish now carries the generated application PRI required for XAML loading. ARM64 execution still requires its native CI runner. Smoke testing loads services and builds the window without applying cursors; it does not validate interactive behavior.
- **No bundled sets yet.** The gallery shows the schemes included with Windows plus imported sets. The signature and openly licensed sets are a separate piece of work.
- **Pointer size changes** write `CursorBaseSize` and reload cursors. This matches what the Windows size slider stores, but whether every Windows build picks the new size up without signing out is Unverified.
- **Screen-inverting pixels** (used by some classic monochrome cursors) are previewed as dark translucent pixels; Windows draws them by inverting the screen.
- **Compressed bitmap data** (RLE) inside cursor files is not supported; such files are skipped at import with a message. PNG-compressed images are supported.
- **Undo is one level deep**: it reverts the most recent journaled apply, rename, or delete. Deleted set files remain in the private library-local `.undo` store until undo or the next journaled operation. If another set occupies the original directory, restoration refuses to overwrite it and retains the undo copy. File/registry ordering and recovery are covered by Core tests; interactive recovery on Windows remains Unverified.
- **Deleting a set in use is refused**, because Windows reads its files directly. Apply another set or reset to default first.
- **Renaming a set** re-registers its Windows scheme under the new name; the old scheme entry is removed.
