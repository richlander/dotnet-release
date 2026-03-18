---
name: update-distro-packages
description: Generate and update per-distro package files that document .NET dependencies and package availability for each Linux distribution. Replaces os-packages.json with one file per distro.
---

# Update Distro Packages

Generate and maintain per-distro JSON files in `release-notes/{version}/distro-packages/`. This is the canonical format for Linux distribution package data, replacing the monolithic `os-packages.json`.

## Format

One file per distribution (e.g. `ubuntu.json`, `alpine.json`). Each file contains:

- **`install_command`** — how to install packages on this distro (e.g. `apt-get install -y {packages}`)
- Per release:
  - **`dependencies`** — packages .NET needs at runtime, with an agnostic `id` (e.g. `libicu`) and distro-specific `name` (e.g. `libicu74`)
  - **`dotnet_packages`** — .NET packages available from the distro's primary/built-in feed
  - **`dotnet_packages_other`** — .NET packages from secondary feeds (keyed by feed name, each with its own `install_command`)

Example (`ubuntu.json`):
```json
{
  "name": "Ubuntu",
  "install_command": "apt-get install -y {packages}",
  "releases": [
    {
      "name": "Ubuntu 24.04 (Noble Numbat)",
      "release": "24.04",
      "dependencies": [
        { "id": "libicu", "name": "libicu74" },
        { "id": "openssl", "name": "libssl3t64" }
      ],
      "dotnet_packages": [
        { "component": "sdk", "name": "dotnet-sdk-10.0" },
        { "component": "runtime", "name": "dotnet-runtime-10.0" }
      ],
      "dotnet_packages_other": {
        "backports": {
          "install_command": "# See distro documentation for backports feed setup",
          "packages": [
            { "component": "sdk", "name": "dotnet-sdk-10.0" }
          ]
        }
      }
    }
  ]
}
```

## When to use

- A new .NET version is released and needs distro package data
- A distro release is added or removed from the support matrix
- Dependencies change for a distro release (e.g. new libicu version)
- A distro ships or drops .NET packages in their archive
- Periodic audit to keep the data current

## Prerequisites

The `dotnet-release` tool is installed globally. Run `dotnet-release --help` to confirm.

## Inputs

The user provides:
- **dotnet/core path** — local path to the dotnet/core repo (e.g. `~/git/core`)
- **.NET version** — which version to generate for (e.g. "10.0")
- Optionally, whether to include .NET package availability (requires `PKGS_ORG_TOKEN`)

## Process

### 1. Optionally query .NET package feeds

If the user wants to include .NET package availability data, run:

```
dotnet-release query distro-packages --dotnet-version {version} --output {path-to-core}/release-notes/{version}/distro-packages.json
```

This queries pkgs.org and supplemental feeds to produce a temporary `distro-packages.json` file listing which .NET packages (SDK, runtime, ASP.NET Core) are available in each distro's archive. Requires `PKGS_ORG_TOKEN`.

If skipped, the generate step produces per-distro files with dependencies only (no `dotnet_packages`).

### 2. Generate per-distro files

```
dotnet-release generate distro-packages {version} {path-to-core}/release-notes
```

This reads `os-packages.json` (for dependency data, during the migration period) and the optional `distro-packages.json` query results, then writes one JSON file per distribution to `release-notes/{version}/distro-packages/`.

File names are derived from distro names: lowercase, spaces replaced with `_` (e.g. `azure_linux.json`, `centos_stream.json`).

### 3. Review results

- Every supported distro has a corresponding file in `distro-packages/`
- Dependencies have correct agnostic `id` and distro-specific `name` per release
- If enriched, `dotnet_packages` shows the built-in feed packages and `dotnet_packages_other` shows secondary feeds

### 4. Commit

Commit all files in `distro-packages/` with a descriptive message.

## Key facts

- This format replaces `os-packages.json` — one file per distro instead of one monolithic file
- Dependencies use an agnostic `id` (e.g. `libicu`) so consumers can match across distros, with a distro-specific `name` (e.g. `libicu74`) for actual install
- `dotnet_packages` is the primary feed; `dotnet_packages_other` is a dictionary of named secondary feeds, each with its own `install_command`
- Without query data, files contain dependencies only — this is valid
- Package names like `libicu` are versioned on Debian/Ubuntu (e.g. `libicu76`) but not on Fedora/RHEL/SUSE (just `libicu`)
- Alpine uses a different naming scheme: `dotnet{major}-{component}` (e.g. `dotnet9-sdk`)
- Microsoft is phasing out packages.microsoft.com for Ubuntu 24.04+ and newer Fedora — those distros ship .NET in their own archives
