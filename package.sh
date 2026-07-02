#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
VERSION="$(<"$SCRIPT_DIR/VERSION")"
OUTPUT="${1:-$SCRIPT_DIR/../RealRim-Water-and-Pumps-${VERSION}.zip}"

"$SCRIPT_DIR/build.sh"
rm -f "$OUTPUT"
(
	cd "$SCRIPT_DIR/.."
	zip -r "$OUTPUT" "$(basename "$SCRIPT_DIR")" \
		-x '*/Source/bin/*' '*/Source/obj/*' '*/.git/*'
)

echo "Packaged: $OUTPUT"
