#!/usr/bin/env bash
set -euo pipefail

# Install dotnet-release tool globally from a local build.
# Usage: ./install.sh

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
TOOL_PROJECT="$REPO_ROOT/src/Dotnet.Release.Tools/Dotnet.Release.Tools.csproj"
PACKAGE_ID="Dotnet.Release.Tools"
NUPKG_DIR="$REPO_ROOT/artifacts/package/release"

echo "Packing $PACKAGE_ID..."
dotnet pack "$TOOL_PROJECT" --nologo -v q -p:ToolPackageRuntimeIdentifiers=

NUPKG=$(ls "$NUPKG_DIR"/$PACKAGE_ID.*.nupkg 2>/dev/null | head -1)
if [ -z "$NUPKG" ]; then
    echo "Error: No .nupkg found in $NUPKG_DIR" >&2
    exit 1
fi

echo "Installing $NUPKG..."
dotnet tool install -g "$PACKAGE_ID" --add-source "$NUPKG_DIR" --prerelease 2>/dev/null \
    || dotnet tool update -g "$PACKAGE_ID" --add-source "$NUPKG_DIR" --prerelease

echo "Installed: $(dotnet-release 2>&1 | head -1 || true)"
echo "Done."
