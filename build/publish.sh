#!/usr/bin/env bash
# Builds the macOS command line binary and app bundle for both architectures into artifacts/.
set -euo pipefail

cd "$(dirname "$0")/.."

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -1)
ARTIFACTS="artifacts"

rm -rf "$ARTIFACTS"
mkdir -p "$ARTIFACTS"

for RID in osx-arm64 osx-x64; do
  NAME="rh-transfer-$VERSION-$RID"
  STAGE="$ARTIFACTS/$NAME"
  GUI="$ARTIFACTS/.gui-$RID"

  echo "==> $RID"
  mkdir -p "$STAGE"

  # Ahead of time compilation already produces one native binary, so no single file switch.
  dotnet publish src/RhTransfer.Cli \
    -c Release -r "$RID" \
    -o "$STAGE" --nologo -v quiet

  dotnet publish src/RhTransfer.Gui \
    -c Release -r "$RID" --self-contained \
    -o "$GUI" --nologo -v quiet

  cp -R "$GUI"/*.app "$STAGE/"
  rm -rf "$GUI"

  # Ahead of time compilation leaves its debug symbols beside the binary, and they are bigger
  # than everything else put together.
  rm -rf "$STAGE"/*.dsym

  # ditto rather than zip: it keeps the bundle's symlinks and executable bits intact.
  # No --sequesterRsrc, which would add a __MACOSX folder and is what Apple's notarisation
  # instructions leave out too.
  ditto -c -k --keepParent "$STAGE" "$ARTIFACTS/$NAME.zip"
done

echo
echo "Version $VERSION"
ls -1 "$ARTIFACTS"/*.zip

cat <<'NOTE'

Sign and notarise before sending a build to anyone, or Gatekeeper will refuse to open it:

  codesign --deep --force --options runtime --timestamp \
    --sign "Developer ID Application: ..." "artifacts/<name>/Rhino Settings Transfer.app"
  xcrun notarytool submit <zip> --wait ...
  xcrun stapler staple "artifacts/<name>/Rhino Settings Transfer.app"
NOTE
