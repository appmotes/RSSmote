#!/usr/bin/env bash
# Shared by the scripts in build/ (A24): the folders, the published browser app and the icons.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS="$ROOT/artifacts"
WEB="$ARTIFACTS/web"
ICONS="$ARTIFACTS/work/icons"

# The browser app, published into $WEB/wwwroot: trimmed, relinked and compressed, as the server serves it.
web() {
  rm -rf "$WEB"
  dotnet publish "$ROOT/src/RSSmote.Browser/RSSmote.Browser.csproj" -c Release -o "$WEB" -v q -clp:ErrorsOnly
}

# Renders the app icon from its SVG master (D13) into $ICONS: PNGs from 16 to 1024 and RSSmote.ico.
icons() {
  mkdir -p "$ICONS"
  dotnet run "$ROOT/build/AppIcons.cs" -- "$ROOT/src/RSSmote.App/Assets/AppIcon/AppIcon.svg" "$ICONS"
}
