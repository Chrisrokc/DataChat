#!/usr/bin/env bash
# Builds DataChat-Installer.pkg from the self-contained publishes in build/out/.
# Requires: pkgbuild + productbuild (Xcode command-line tools).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUT_DIR="$REPO_ROOT/build/out"
PKG_STAGE="$REPO_ROOT/build/pkg-stage"
PKG_OUT="$REPO_ROOT/build/installers"
VERSION="${VERSION:-1.0.0}"
IDENTIFIER="com.datachat.app"

mkdir -p "$PKG_OUT"
rm -rf "$PKG_STAGE"
mkdir -p "$PKG_STAGE/payload/Applications/DataChat/bin/osx-arm64"
mkdir -p "$PKG_STAGE/payload/Applications/DataChat/bin/osx-x64"
mkdir -p "$PKG_STAGE/payload/Library/LaunchAgents"

if [ ! -d "$OUT_DIR/osx-arm64" ] || [ ! -d "$OUT_DIR/osx-x64" ]; then
  echo "ERROR: missing $OUT_DIR/osx-arm64 or $OUT_DIR/osx-x64"
  echo "Run ./build/publish.sh osx-arm64 osx-x64 first."
  exit 1
fi

echo "==> Staging payload"
cp -R "$OUT_DIR/osx-arm64/"* "$PKG_STAGE/payload/Applications/DataChat/bin/osx-arm64/"
cp -R "$OUT_DIR/osx-x64/"*   "$PKG_STAGE/payload/Applications/DataChat/bin/osx-x64/"
cp "$SCRIPT_DIR/LaunchAgent/com.datachat.app.plist" "$PKG_STAGE/payload/Library/LaunchAgents/com.datachat.app.plist"
cp "$SCRIPT_DIR/uninstall.command" "$PKG_STAGE/payload/Applications/DataChat/uninstall.command" 2>/dev/null || true

chmod +x "$PKG_STAGE/payload/Applications/DataChat/bin/osx-arm64/DataChat.Web" || true
chmod +x "$PKG_STAGE/payload/Applications/DataChat/bin/osx-x64/DataChat.Web"   || true

mkdir -p "$PKG_STAGE/scripts"
cp "$SCRIPT_DIR/scripts/preinstall"  "$PKG_STAGE/scripts/preinstall"
cp "$SCRIPT_DIR/scripts/postinstall" "$PKG_STAGE/scripts/postinstall"
chmod +x "$PKG_STAGE/scripts/preinstall" "$PKG_STAGE/scripts/postinstall"

echo "==> Building component pkg"
pkgbuild \
  --root "$PKG_STAGE/payload" \
  --scripts "$PKG_STAGE/scripts" \
  --identifier "$IDENTIFIER" \
  --version "$VERSION" \
  --install-location "/" \
  "$PKG_STAGE/DataChat-component.pkg"

echo "==> Building distribution pkg"
productbuild \
  --distribution "$SCRIPT_DIR/distribution.xml" \
  --package-path "$PKG_STAGE" \
  --resources "$SCRIPT_DIR/resources" \
  "$PKG_OUT/DataChat-Installer-$VERSION.pkg"

echo "==> Done: $PKG_OUT/DataChat-Installer-$VERSION.pkg"
