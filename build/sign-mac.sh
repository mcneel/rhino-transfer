#!/usr/bin/env bash
# Signs, notarises and staples a published macOS folder so Gatekeeper opens it without warning.
# The gui and cli are published to separate folders, so run it once over each.
#
#   RHT_SIGN_IDENTITY   "Developer ID Application: Robert McNeel & Associates (TEAMID)"
#   RHT_NOTARY_PROFILE  a notarytool keychain profile, created once with:
#                         xcrun notarytool store-credentials <name> \
#                           --apple-id <id> --team-id <team> --password <app-specific-password>
#
# With neither set the publish stays ad-hoc signed: it opens, but only via right click and Open.
set -euo pipefail

STAGE=${1:?usage: sign-mac.sh <a published folder holding the .app or the cli>}
IDENTITY=${RHT_SIGN_IDENTITY:-}
PROFILE=${RHT_NOTARY_PROFILE:-}

cd "$(dirname "$0")/.."
ENTITLEMENTS="$PWD/build/entitlements.plist"

if [ -z "$IDENTITY" ]; then
  echo "  no RHT_SIGN_IDENTITY, leaving the ad-hoc signature in place"
  exit 0
fi

APP=$(find "$STAGE" -maxdepth 1 -name "*.app" | head -1)

# Inner binaries first, outermost last: a signature covers what is inside it, so signing the
# bundle before its dylibs invalidates the bundle.
if [ -n "$APP" ]; then
  find "$APP" -type f \( -name "*.dylib" -o -name "*.so" \) -print0 |
    while IFS= read -r -d '' binary; do
      codesign --force --timestamp --options runtime --sign "$IDENTITY" "$binary"
    done

  codesign --force --timestamp --options runtime --entitlements "$ENTITLEMENTS" \
    --sign "$IDENTITY" "$APP"
  codesign --verify --deep --strict --verbose=2 "$APP"
fi

for cli in "$STAGE"/rh-transfer; do
  [ -f "$cli" ] || continue
  codesign --force --timestamp --options runtime --sign "$IDENTITY" "$cli"
done

if [ -z "$PROFILE" ]; then
  echo "  signed but not notarised: set RHT_NOTARY_PROFILE to finish the job"
  exit 0
fi

# Notarisation takes a zip of what is being shipped, then the ticket is stapled to the app so it
# works offline.
NOTARY_ZIP=$(mktemp -d)/notarise.zip
ditto -c -k --keepParent "$STAGE" "$NOTARY_ZIP"

xcrun notarytool submit "$NOTARY_ZIP" --keychain-profile "$PROFILE" --wait

[ -n "$APP" ] && xcrun stapler staple "$APP"

echo "  signed, notarised and stapled"
