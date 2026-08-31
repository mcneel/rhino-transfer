#!/usr/bin/env bash
# Builds the macOS app bundles and command line binaries into artifacts/.
set -euo pipefail

cd "$(dirname "$0")/.."

ARTIFACTS="artifacts"
rm -rf "$ARTIFACTS"
mkdir -p "$ARTIFACTS"

for RID in osx-arm64 osx-x64; do
  echo "==> $RID"

  dotnet publish src/RhTransfer.Cli \
    -c Release -r "$RID" --self-contained \
    -p:PublishSingleFile=true \
    -o "$ARTIFACTS/$RID" \
    --nologo -v quiet

  dotnet publish src/RhTransfer.Gui \
    -c Release -r "$RID" --self-contained \
    -o "$ARTIFACTS/gui-$RID" \
    --nologo -v quiet

  APP=$(find "$ARTIFACTS/gui-$RID" -maxdepth 2 -name "*.app" -print -quit)
  if [ -n "$APP" ]; then
    cp -R "$APP" "$ARTIFACTS/$RID/"
  fi
  rm -rf "$ARTIFACTS/gui-$RID"
done

echo
echo "Built:"
find "$ARTIFACTS" -maxdepth 2 -name "rh-transfer" -o -maxdepth 2 -name "*.app" | sort

cat <<'NOTE'

Before sending a build to anyone, sign and notarise the .app:

  codesign --deep --force --options runtime --timestamp \
    --sign "Developer ID Application: ..." artifacts/osx-arm64/RhTransfer.Gui.app
  xcrun notarytool submit ... --wait
  xcrun stapler staple artifacts/osx-arm64/RhTransfer.Gui.app

Without this Gatekeeper refuses to open it.
NOTE
