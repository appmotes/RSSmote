#!/usr/bin/env bash
# The server with the browser app beside it, ready to copy to a machine (A24), in artifacts/server.
#   build/publish.sh               needs the .NET 10 ASP.NET Core runtime where it runs
#   build/publish.sh linux-x64     brings its own runtime, for that platform
# Run it there with RSSMOTE_KEY set: dotnet RSSmote.Server.dll, or ./RSSmote.Server when self-contained.
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
out="$ARTIFACTS/server"
web
rm -rf "$out"
if [[ -n "${1:-}" ]]; then
  dotnet publish "$ROOT/src/RSSmote.Server/RSSmote.Server.csproj" -c Release -r "$1" --self-contained -o "$out" -v q -clp:ErrorsOnly
else
  dotnet publish "$ROOT/src/RSSmote.Server/RSSmote.Server.csproj" -c Release -o "$out" -v q -clp:ErrorsOnly
fi
cp -R "$WEB/wwwroot" "$out/wwwroot"
echo "Published to $out"
