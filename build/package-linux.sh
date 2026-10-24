#!/usr/bin/env bash
# Linux (A2): a tarball per architecture holding an RSSmote folder with the executable, its icon and a .desktop
# entry. Runs on macOS or Linux.
# Usage: build/package-linux.sh [linux-x64] [linux-arm64]
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
icons

rids=("$@")
[[ ${#rids[@]} -gt 0 ]] || rids=(linux-x64 linux-arm64)
for rid in "${rids[@]}"; do
  dir="$WORK/$rid/RSSmote"
  publish "$rid" "$dir"
  cp "$ICONS/icon-256.png" "$dir/rssmote.png"
  # Exec and Icon are absolute paths in a desktop entry; the README says where to unpack and how to fix them.
  cat > "$dir/rssmote.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=RSSmote
Comment=RSS reader
Exec=/opt/RSSmote/RSSmote
Icon=/opt/RSSmote/rssmote.png
Categories=Network;News;
Terminal=false
DESKTOP
  tarball="$OUT/RSSmote-$VERSION-linux-${rid#linux-}.tar.gz"
  # No AppleDouble files when packed on macOS.
  COPYFILE_DISABLE=1 tar -czf "$tarball" -C "$WORK/$rid" RSSmote
  echo "$tarball"
done
