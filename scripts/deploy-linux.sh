#!/usr/bin/env bash
# Deploy MiniCAD: publish a self-contained, single-file Linux build and drop the
# executable into ~/Applications so it can be launched standalone without the SDK.
#
# This is the .NET/Avalonia analog of Abyss' deploy-appimage.sh: instead of an
# electron-builder AppImage we produce a self-contained single-file binary via
# `dotnet publish` and atomically swap it into place.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

PROJECT="src/MiniCAD.App/MiniCAD.App.csproj"
CONFIG="Release"
RID="linux-x64"
PUBLISH_DIR="src/MiniCAD.App/bin/${CONFIG}/net10.0/${RID}/publish"
DEST="${HOME}/Applications/MiniCAD"

echo "Publishing ${PROJECT} (${CONFIG}, ${RID}, self-contained single-file)…"
dotnet publish "$PROJECT" \
  -c "$CONFIG" \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none \
  --nologo

# The apphost is named after the assembly (MiniCAD.App), no extension on Linux.
SRC="${PUBLISH_DIR}/MiniCAD.App"
if [[ ! -f "$SRC" ]]; then
  echo "Deploy failed: expected executable not found at ${SRC}" >&2
  echo "Did the publish succeed?" >&2
  exit 1
fi

mkdir -p "$(dirname "$DEST")"

# Atomic replace: copy to a temp file next to the target, then rename over it. A
# running instance keeps its old inode; the next launch opens the new binary.
cp "$SRC" "${DEST}.tmp"
chmod +x "${DEST}.tmp"
mv -f "${DEST}.tmp" "$DEST"

echo "Deployed ${SRC} -> ${DEST}"
