#!/usr/bin/env bash
# Decompiles the game into .decompiled/ for reference while developing. The RimWorld EULA allows
# this for learning and modding, but forbids sharing the result, so the folder is git-ignored.
set -euo pipefail

ILSPY_VERSION="11.2.0.10050"

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rimworld="${RIMWORLD_DIR:-$HOME/.local/share/Steam/steamapps/common/RimWorld}"
managed="$rimworld/RimWorldLinux_Data/Managed"
[[ -d "$managed" ]] || managed="$rimworld/RimWorldWin64_Data/Managed"
[[ -d "$managed" ]] || managed="$rimworld/RimWorldMac.app/Contents/Resources/Data/Managed"

if [[ ! -f "$managed/Assembly-CSharp.dll" ]]; then
  echo "The game's assemblies were not found. Set RIMWORLD_DIR to the game's folder." >&2
  exit 1
fi

tool="$repo/.tools/ilspycmd"
if [[ ! -x "$tool" ]]; then
  dotnet tool install ilspycmd --version "$ILSPY_VERSION" --tool-path "$repo/.tools"
fi

output="$repo/.decompiled/game"
rm -rf "$output"
mkdir -p "$output"
"$tool" -p -o "$output" "$managed/Assembly-CSharp.dll" -r "$managed"
echo "Decompiled to: $output"
