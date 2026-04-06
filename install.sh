#!/usr/bin/env bash
set -euo pipefail

# Packs and installs the public and maintainer release tools from local source.
# Usage: ./install.sh

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
NUPKG_DIR="$REPO_ROOT/artifacts/package/release"
TOOLS=(
    "$REPO_ROOT/src/Dotnet.Release.Tool/Dotnet.Release.Tool.csproj|Dotnet.Release.Tool|dotnet-release"
    "$REPO_ROOT/src/Dotnet.Release.Tools/Dotnet.Release.Tools.csproj|ReleaseNotes.Gen|release-notes-gen"
)

echo "=== Installing release tools from source ==="

# Uninstall if already installed
for tool in "${TOOLS[@]}"; do
    IFS='|' read -r _ package_id command_name <<< "$tool"
    if dotnet tool list -g | grep -q "$package_id"; then
        echo "Uninstalling existing $command_name..."
        dotnet tool uninstall -g "$package_id"
    fi
done

# Clean previous packages
rm -rf "$NUPKG_DIR"

# Pack
echo "Packing..."
for tool in "${TOOLS[@]}"; do
    IFS='|' read -r tool_project _ _ <<< "$tool"
    dotnet pack "$tool_project" -o "$NUPKG_DIR" -p:OfficialBuild=true
    dotnet pack "$tool_project" -o "$NUPKG_DIR" -p:OfficialAotBuild=true
done

# Install from local packages
echo "Installing..."
for tool in "${TOOLS[@]}"; do
    IFS='|' read -r _ package_id command_name <<< "$tool"
    dotnet tool install -g "$package_id" --add-source "$NUPKG_DIR" --prerelease
    echo "Installed $command_name"
done

echo ""
echo "Done. Run 'dotnet-release' and 'release-notes-gen --help' to verify."
