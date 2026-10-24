#!/usr/bin/env bash
# macOS (A2): RSSmote.app per architecture, ad-hoc signed, zipped. Runs on macOS (iconutil, codesign, ditto).
# Usage: build/package-macos.sh [osx-arm64] [osx-x64]
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
icons

rids=("$@")
[[ ${#rids[@]} -gt 0 ]] || rids=(osx-arm64 osx-x64)
for rid in "${rids[@]}"; do
  app="$WORK/$rid/RSSmote.app"
  rm -rf "$app"
  publish "$rid" "$app/Contents/MacOS"
  mkdir -p "$app/Contents/Resources"

  iconset="$WORK/$rid/RSSmote.iconset"
  rm -rf "$iconset" && mkdir -p "$iconset"
  for size in 16 32 128 256 512; do
    cp "$ICONS/icon-$size.png" "$iconset/icon_${size}x${size}.png"
    cp "$ICONS/icon-$((size * 2)).png" "$iconset/icon_${size}x${size}@2x.png"
  done
  iconutil -c icns -o "$app/Contents/Resources/RSSmote.icns" "$iconset"

  # Web content may load http images (N1); everything else keeps App Transport Security.
  cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>RSSmote</string>
  <key>CFBundleDisplayName</key><string>RSSmote</string>
  <key>CFBundleIdentifier</key><string>com.ahrentlov.rssmote</string>
  <key>CFBundleExecutable</key><string>RSSmote</string>
  <key>CFBundleIconFile</key><string>RSSmote</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>LSMinimumSystemVersion</key><string>14.0</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.news</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
  <key>NSAppTransportSecurity</key>
  <dict><key>NSAllowsArbitraryLoadsInWebContent</key><true/></dict>
</dict>
</plist>
PLIST

  codesign --force --deep --sign - "$app"
  zip="$OUT/RSSmote-$VERSION-macos-${rid#osx-}.zip"
  rm -f "$zip"
  ditto -c -k --sequesterRsrc --keepParent "$app" "$zip"
  echo "$zip"
done
