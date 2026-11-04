#!/usr/bin/env bash
# Builds the mod and the MCP server, then copies the mod into the game's Mods folder.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -z "${RIMWORLD_DIR:-}" ]]; then
  for candidate in \
    "$HOME/.local/share/Steam/steamapps/common/RimWorld" \
    "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/RimWorld" \
    "$HOME/Library/Application Support/Steam/steamapps/common/RimWorld"; do
    if [[ -d "$candidate" ]]; then
      RIMWORLD_DIR="$candidate"
      break
    fi
  done
fi

if [[ -z "${RIMWORLD_DIR:-}" || ! -d "$RIMWORLD_DIR/Mods" ]]; then
  echo "RimWorld was not found. Set RIMWORLD_DIR to the game's folder and run this again." >&2
  exit 1
fi
export RIMWORLD_DIR

dotnet build "$repo/mod/Source" -c Release -nologo -v quiet

(
  cd "$repo/server"
  pnpm install --frozen-lockfile
  pnpm build
)

target="$RIMWORLD_DIR/Mods/RimWorldMCP"
rm -rf "$target"
mkdir -p "$target/1.6"
cp -r "$repo/mod/About" "$target/"
cp -r "$repo/mod/1.6/Assemblies" "$target/1.6/"
cp "$repo/LICENSE" "$target/"

echo
echo "Mod installed to: $target"
echo "MCP server built: $repo/server/dist/index.js"
