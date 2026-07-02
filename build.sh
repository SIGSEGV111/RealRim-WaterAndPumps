#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

if ! command -v dotnet >/dev/null 2>&1
then
	echo "Error: dotnet was not found in PATH." >&2
	exit 1
fi

rm -rf "$SCRIPT_DIR/Source/bin" "$SCRIPT_DIR/Source/obj"
rm -f \
	"$SCRIPT_DIR/Assemblies/RealRim.WaterAndPumps.dll" \
	"$SCRIPT_DIR/Assemblies/RealRim.WaterAndPumps.pdb"

dotnet restore "$SCRIPT_DIR/Source/RealRim.WaterAndPumps.csproj"
dotnet build "$SCRIPT_DIR/Source/RealRim.WaterAndPumps.csproj" --configuration Release --no-restore

echo "Built: $SCRIPT_DIR/Assemblies/RealRim.WaterAndPumps.dll"
