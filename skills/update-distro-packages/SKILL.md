---
name: update-distro-packages
description: Update per-distro package files in release-notes/<version>/distros/ documenting dependencies and .NET package availability for each Linux distribution.
---

# Update Distro Packages

Maintain per-distro package files under `release-notes/<version>/distros/` in the dotnet/core repo. These files document what packages each Linux distribution needs to run .NET and which .NET packages are available in each distro's archive.

## When to use

- A new .NET version is being set up (seed from previous version)
- A new distro release ships (e.g. Ubuntu 28.04) and needs adding
- Periodic audit of .NET package availability across distros
- Dependencies may have changed between .NET versions

## File layout

```
release-notes/11.0/distros/
  index.json            # plain index listing all distro files
  dependencies.json     # hand-maintained, distro-agnostic package list
  ubuntu.json           # per-distro: dependencies + dotnet packages
  debian.json
  alpine.json
  ...
```

## Prerequisites

The `dotnet-release` tool must be installed globally. It provides the pkgs.org query client:

```bash
dotnet-release query distro-packages --dotnet-version 11.0
```

Requires `PKGS_ORG_TOKEN` environment variable (pkgs.org Gold+ subscription).

## Inputs

The user provides:
- **dotnet/core path** — local path to the dotnet/core repo (e.g. `~/git/core`)
- **.NET version** — which version to work on (e.g. "11.0")
- Optionally, specific distros or tasks to focus on

## Process

### 1. New .NET version setup

When setting up distros/ for a new version (e.g. 12.0):

1. Read the previous version's `distros/dependencies.json`
2. Ask the user whether the dependency list has changed for the new version (new dependencies, removed dependencies, changed minimum versions)
3. Create `dependencies.json` for the new version, updating as needed
4. Copy per-distro files from the previous version as a starting point
5. Update `distros/index.json` to list all files
6. The dependency mappings (e.g. Ubuntu 24.04 uses `libicu74` for `libicu`) carry forward unchanged

### 2. Adding a new distro release

When a new distro release ships (e.g. Ubuntu 28.04):

1. Read the existing distro file (e.g. `ubuntu.json`)
2. Query pkgs.org for the new release's dependency package names:
   - Look up each package ID from `dependencies.json` (libc, openssl, libicu, etc.)
   - Find the concrete package name in the new release
3. Add the new release entry with its dependencies
4. Query for .NET package availability in the new release
5. Keep releases ordered by version (newest first in the file)

### 3. Updating .NET package availability

1. Run `dotnet-release query distro-packages --dotnet-version <ver>` to get current package data from pkgs.org
2. For each distro file, update `dotnet_packages` (built-in feed) and `dotnet_packages_other` (alternative feeds)
3. Update the root `dotnet_versions` array to reflect all available versions
4. Dictionary keys: versions descending, feed names ascending, component names ascending

### 4. Verify and commit

1. Validate JSON files parse correctly
2. Ensure `index.json` lists all distro files
3. Show the user a summary of changes
4. On confirmation, commit

## Schema reference

### dependencies.json (hand-maintained)

```json
{
  "channel_version": "11.0",
  "packages": [
    {
      "id": "openssl",
      "name": "OpenSSL",
      "required_scenarios": ["cryptography", "https"],
      "min_version": "1.1.1",
      "references": ["https://www.openssl.org/"]
    }
  ]
}
```

### Per-distro file (e.g. ubuntu.json)

```json
{
  "name": "Ubuntu",
  "dotnet_versions": ["10.0", "9.0"],
  "install_command": "apt-get install -y {packages}",
  "releases": [
    {
      "name": "Ubuntu 24.04 (Noble Numbat)",
      "release": "24.04",
      "dependencies": [
        { "id": "libc", "name": "libc6" },
        { "id": "openssl", "name": "libssl3t64" }
      ],
      "dotnet_packages": {
        "9.0": [
          { "component": "runtime", "name": "dotnet-runtime-9.0" },
          { "component": "sdk", "name": "dotnet-sdk-9.0" }
        ]
      },
      "dotnet_packages_other": {
        "microsoft": {
          "install_command": "...",
          "packages": {
            "10.0": [
              { "component": "sdk", "name": "dotnet-sdk-10.0" }
            ]
          }
        }
      }
    }
  ]
}
```

### index.json

```json
{
  "channel_version": "11.0",
  "distros": ["alpine.json", "debian.json", "ubuntu.json"]
}
```

## Naming and ordering conventions

- File names use snake_case (e.g. `azure_linux.json`, `centos_stream.json`, `opensuse_leap.json`)
- JSON property names use snake_case (handled by serializer)
- Alpha ascending for names/strings (dependency IDs, component names, feed names)
- Descending for numbers/versions (newest first)

## Key facts

- `dependencies.json` is hand-maintained, not generated. It rarely changes between .NET versions.
- Per-distro files are the durable source of truth. For new .NET versions, seed from the previous version.
- Microsoft is phasing out packages.microsoft.com for Ubuntu 24.04+ and newer Fedora
- Alpine uses different naming: `dotnet{major}-{component}` (e.g. `dotnet9-sdk`)
- Alpine packages are in the `community` repo, not `main`
- Use pkgs.org to look up concrete package names for new distro releases
- The `dotnet/docs` repo has authoritative availability tables in `docs/core/install/linux-*.md`
