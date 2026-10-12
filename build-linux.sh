#!/usr/bin/env bash
# Linux build: one self-contained executable (no .NET install needed on the target machine).
# Output: linux/bin/Release/net8.0/linux-x64/publish/client-o-preview
set -euo pipefail
cd "$(dirname "$0")/linux"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
"$DOTNET" publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
