#!/usr/bin/env bash
set -euo pipefail

# Packs and installs dotnet-release as a global tool from local source.
# Usage: ./install.sh

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
TOOL_PROJECT="$REPO_ROOT/src/Dotnet.Release.Tools/Dotnet.Release.Tools.csproj"
PACKAGE_ID="Dotnet.Release.Tools"
NUPKG_DIR="$REPO_ROOT/artifacts/package/release"

echo "=== Installing dotnet-release from source ==="

# Uninstall if already installed
if dotnet tool list -g | grep -q "$PACKAGE_ID"; then
    echo "Uninstalling existing dotnet-release..."
    dotnet tool uninstall -g "$PACKAGE_ID"
fi

# Clean previous packages
rm -rf "$NUPKG_DIR"

# Pack
echo "Packing..."
dotnet pack "$TOOL_PROJECT" -o "$NUPKG_DIR" -p:OfficialBuild=true
dotnet pack "$TOOL_PROJECT" -o "$NUPKG_DIR" -p:OfficialAotBuild=true

# Install from local packages
echo "Installing..."
dotnet tool install -g "$PACKAGE_ID" --add-source "$NUPKG_DIR" --prerelease

echo ""
echo "Done. Run 'dotnet-release --help' to verify."
