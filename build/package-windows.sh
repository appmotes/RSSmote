#!/usr/bin/env bash
# Windows (A2): a zip per architecture holding an RSSmote folder with RSSmote.exe. Runs on macOS or Linux.
# Usage: build/package-windows.sh [win-x64] [win-arm64]
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
icons

rids=("$@")
[[ ${#rids[@]} -gt 0 ]] || rids=(win-x64 win-arm64)
for rid in "${rids[@]}"; do
  publish "$rid" "$WORK/$rid/RSSmote"
  zip="$OUT/RSSmote-$VERSION-windows-${rid#win-}.zip"
  rm -f "$zip"
  (cd "$WORK/$rid" && zip -qrX "$zip" RSSmote)
  echo "$zip"
done
