#!/usr/bin/env bash
# Writes the app icon (D13) everywhere a build or the README reads it from the repository; run it after AppIcon.svg
# changes and commit what it writes. App: the desktop window's icon and the one beside the version line in Settings.
# Desktop: the .exe's icon. iOS: one opaque 1024 square in an asset catalog. Android: an adaptive icon (the accent
# behind the marks) for API 26 and later, and the rounded tile for API 24 and 25. README: the icon over the title.
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
icons

desktop="$ROOT/src/RSSmote.Desktop/Assets"
mkdir -p "$desktop"
cp "$ICONS/RSSmote.ico" "$desktop/AppIcon.ico"
cp "$ICONS/icon-256.png" "$ROOT/src/RSSmote.App/Assets/AppIcon/AppIcon.png"
cp "$ICONS/icon-256.png" "$ROOT/Docs/images/app-icon.png"

ios="$ROOT/src/RSSmote.iOS/Assets.xcassets/AppIcon.appiconset"
mkdir -p "$ios"
cp "$ICONS/ios-1024.png" "$ios/AppIcon.png"
cat > "$ios/Contents.json" <<JSON
{
  "images": [{ "filename": "AppIcon.png", "idiom": "universal", "platform": "ios", "size": "1024x1024" }],
  "info": { "author": "xcode", "version": 1 }
}
JSON

res="$ROOT/src/RSSmote.Android/Resources"
densities=(mdpi hdpi xhdpi xxhdpi xxxhdpi)
legacy=(48 72 96 144 192)
foreground=(108 162 216 324 432)
for i in "${!densities[@]}"; do
  dir="$res/mipmap-${densities[$i]}"
  mkdir -p "$dir"
  cp "$ICONS/icon-${legacy[$i]}.png" "$dir/appicon.png"
  cp "$ICONS/android-foreground-${foreground[$i]}.png" "$dir/appicon_foreground.png"
done
mkdir -p "$res/mipmap-anydpi-v26" "$res/values"
cat > "$res/mipmap-anydpi-v26/appicon.xml" <<XML
<?xml version="1.0" encoding="utf-8"?>
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
  <background android:drawable="@color/appicon_background" />
  <foreground android:drawable="@mipmap/appicon_foreground" />
</adaptive-icon>
XML
cat > "$res/values/appicon_background.xml" <<XML
<?xml version="1.0" encoding="utf-8"?>
<resources>
  <color name="appicon_background">$(cat "$ICONS/android-background.txt")</color>
</resources>
XML
echo "Icons written"
