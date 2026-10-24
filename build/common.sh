#!/usr/bin/env bash
# Shared by the package-*.sh scripts (A2): the version, the folders, a self-contained publish and the icons.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/Directory.Build.props")"
OUT="$ROOT/artifacts/packages"
WORK="$ROOT/artifacts/work"
ICONS="$WORK/icons"
mkdir -p "$OUT" "$WORK"

# Renders the app icon from its SVG master (D13) into $ICONS: PNGs from 16 to 1024 and RSSmote.ico.
icons() {
  dotnet run "$ROOT/build/AppIcons.cs" -- "$ROOT/src/RSSmote.App/Assets/AppIcon/AppIcon.svg" "$ICONS"
}

# publish <rid> <folder>: the desktop app with its own .NET runtime and no symbols.
publish() {
  rm -rf "$2"
  dotnet publish "$ROOT/src/RSSmote.Desktop/RSSmote.Desktop.csproj" -c Release -r "$1" --self-contained \
    -p:DebugType=None -o "$2" -v q -clp:ErrorsOnly
  # Native packages bring their own symbols, which DebugType does not cover.
  find "$2" -name '*.pdb' -delete
}
