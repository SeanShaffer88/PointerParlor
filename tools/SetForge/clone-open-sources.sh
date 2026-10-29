#!/bin/sh
# Clones the openly licensed cursor themes that SetForge converts, into the folder given (default: ../open-sources).
# Then run: dotnet run -c Release --project tools/SetForge -- --open <that folder>
set -e
dest="${1:-../open-sources}"
mkdir -p "$dest"
for repo in \
  ful1e5/Bibata_Cursor ful1e5/fuchsia-cursor ful1e5/banana-cursor ful1e5/XCursor-pro \
  vinceliuice/Graphite-cursors vinceliuice/Layan-cursors vinceliuice/Vimix-cursors \
  yeyushengfan258/Future-cursors guillaumeboehm/Nordzy-cursors
do
  dir="$dest/$(echo "$repo" | tr / _)"
  [ -d "$dir" ] || git clone --depth 1 "https://github.com/$repo" "$dir"
done
