#!/usr/bin/env bash
# Runs RSSmote on this machine (A24): publishes the browser app, then serves it and the API on port 5000.
#   RSSMOTE_KEY=… build/run-local.sh          http://localhost:5000
#   RSSMOTE_KEY=… build/run-local.sh --lan    every address, so a phone on the same network can open it
# RSSMOTE_DATA_DIR picks the library; the per-OS data folder otherwise (A4).
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
: "${RSSMOTE_KEY:?Set RSSMOTE_KEY to the key you will sign in with (at least 12 characters).}"
web
urls="http://localhost:5000"
[[ "${1:-}" == "--lan" ]] && urls="http://0.0.0.0:5000"
RSSMOTE_WEB_DIR="$WEB/wwwroot" ASPNETCORE_URLS="$urls" exec dotnet run --project "$ROOT/src/RSSmote.Server" -c Release
