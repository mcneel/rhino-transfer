#!/usr/bin/env bash
# Builds the macOS command line binary and app bundle for both architectures into artifacts/.
# Each ships as its own archive: a user wants the window or the command line, not both.
set -euo pipefail

cd "$(dirname "$0")/.."

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -1)
ARTIFACTS="artifacts"

rm -rf "$ARTIFACTS"
mkdir -p "$ARTIFACTS"

for RID in osx-arm64 osx-x64; do
  echo "==> $RID"

  CLI="$ARTIFACTS/rh-transfer-cli-$VERSION-$RID"
  GUI="$ARTIFACTS/rh-transfer-gui-$VERSION-$RID"
  SCRATCH="$ARTIFACTS/.gui-build-$RID"
  mkdir -p "$CLI" "$GUI"

  # Ahead of time compilation already produces one native binary, so no single file switch.
  dotnet publish src/RhTransfer.Cli \
    -c Release -r "$RID" \
    -o "$CLI" --nologo -v quiet

  # Ahead of time compilation leaves its debug symbols beside the binary, and they are bigger
  # than everything else put together.
  rm -rf "$CLI"/*.dsym

  # Eto's publish drops the bundle beside the loose build output it was assembled from, so
  # stage it somewhere else and keep only the bundle.
  dotnet publish src/RhTransfer.Gui \
    -c Release -r "$RID" --self-contained \
    -o "$SCRATCH" --nologo -v quiet

  cp -R "$SCRATCH"/*.app "$GUI/"
  rm -rf "$SCRATCH"

  # .NET ad-hoc signs the bare apphost at publish time, then Eto assembles the .app around it,
  # which leaves a signature describing resources that are no longer there. macOS reports that
  # as "damaged and can't be opened", which reads like corruption but is a signature mismatch.
  # Re-signing the finished bundle binds Info.plist and regenerates _CodeSignature.
  codesign --force --deep --sign - "$GUI"/*.app
  codesign --verify --deep --strict "$GUI"/*.app

  # ditto rather than zip: it keeps the bundle's symlinks and executable bits intact.
  # No --sequesterRsrc, which would add a __MACOSX folder and is what Apple's notarisation
  # instructions leave out too.
  #
  # The cli keeps its folder, so a bare rh-transfer does not land loose in Downloads with
  # nothing to say which build it is. The bundle is zipped on its own, because dragging a
  # folder to Applications is not what anyone means to do.
  ditto -c -k --keepParent "$CLI" "$ARTIFACTS/$(basename "$CLI").zip"
  ditto -c -k --keepParent "$GUI"/*.app "$ARTIFACTS/$(basename "$GUI").zip"
done

echo
echo "Version $VERSION"
ls -1 "$ARTIFACTS"/*.zip

cat <<'NOTE'

Sign and notarise before sending a build to anyone, or Gatekeeper will refuse to open it.
Run build/sign-mac.sh over each staged folder, then re-zip it:

  ./build/sign-mac.sh artifacts/rh-transfer-gui-<version>-<rid>
  ./build/sign-mac.sh artifacts/rh-transfer-cli-<version>-<rid>
NOTE
