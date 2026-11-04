#!/usr/bin/env bash
# Writes the app icon (D13) where the app and the README read it from; run it after AppIcon.svg changes and commit
# what it writes. The browser app takes the same file as its page icon and home-screen icon.
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
icons
cp "$ICONS/icon-256.png" "$ROOT/src/RSSmote.App/Assets/AppIcon/AppIcon.png"
cp "$ICONS/icon-256.png" "$ROOT/Docs/images/app-icon.png"
echo "Icons written"
