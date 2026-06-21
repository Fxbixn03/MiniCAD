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

# --- Desktop integration -----------------------------------------------------
# A bare executable in ~/Applications is invisible to the OS application menu;
# the launcher only indexes freedesktop.org .desktop entries. Install one (plus
# an icon into the hicolor theme) so MiniCAD shows up and can be pinned. This is
# idempotent — it is rewritten on every deploy.
APP_ID="minicad"
DESKTOP_DIR="${HOME}/.local/share/applications"
ICON_DIR_SVG="${HOME}/.local/share/icons/hicolor/scalable/apps"
ICON_DIR_PNG="${HOME}/.local/share/icons/hicolor/256x256/apps"
mkdir -p "$DESKTOP_DIR" "$ICON_DIR_SVG" "$ICON_DIR_PNG"

[[ -f "${ROOT}/assets/logo.svg" ]] && cp -f "${ROOT}/assets/logo.svg" "${ICON_DIR_SVG}/${APP_ID}.svg"
[[ -f "${ROOT}/src/MiniCAD.App/Assets/logo.png" ]] && cp -f "${ROOT}/src/MiniCAD.App/Assets/logo.png" "${ICON_DIR_PNG}/${APP_ID}.png"

cat > "${DESKTOP_DIR}/${APP_ID}.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=MiniCAD
GenericName=CAD Editor
Comment=Cross-platform CAD application
Exec=${DEST}
Icon=${APP_ID}
Terminal=false
Categories=Graphics;Engineering;2DGraphics;
StartupNotify=true
StartupWMClass=MiniCAD.App
EOF
chmod 644 "${DESKTOP_DIR}/${APP_ID}.desktop"

# Refresh the menu cache so the entry appears without a logout (best effort).
update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
command -v kbuildsycoca6 >/dev/null 2>&1 && kbuildsycoca6 --noincremental >/dev/null 2>&1 || true

echo "Installed desktop entry -> ${DESKTOP_DIR}/${APP_ID}.desktop"
