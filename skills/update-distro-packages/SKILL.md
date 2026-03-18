---
name: update-distro-packages
description: Generate per-distro package files documenting dependencies and .NET package availability for each Linux distribution, for a given .NET version.
---

# Update Distro Packages

Generate per-distro JSON files in the `distro-packages/` directory that combine dependency information (what packages .NET needs) with .NET package availability (where to get .NET) for each Linux distribution.

## When to use

- A new .NET version is released and you need to document distro package info
- A distro ships or drops a .NET version in their archive
- Periodic audit to keep the availability data current
- Dependencies change for a distro release (e.g. new libicu version)

## Background

Each distro gets its own file (e.g. `ubuntu.json`, `alpine.json`) in `release-notes/{version}/distro-packages/`. Each file contains:

- **`install_command`** — how to install packages on this distro (e.g. `apt-get install -y {packages}`)
- **`dependencies`** — packages .NET needs at runtime, with an agnostic `id` and distro-specific `name` (from `os-packages.json`)
- **`dotnet_packages`** — .NET packages available from the distro's primary/built-in feed
- **`dotnet_packages_other`** — .NET packages from secondary feeds (keyed by feed name, each with its own `install_command`)

Package naming varies by distro:
- Debian/Ubuntu/Fedora: `dotnet-sdk-9.0`, `dotnet-runtime-9.0`, `aspnetcore-runtime-9.0`
- Alpine: `dotnet9-sdk`, `dotnet9-runtime`

## Prerequisites

The `dotnet-release` tool is installed globally. Run `dotnet-release --help` to confirm.

## Inputs

The user provides:
- **dotnet/core path** — local path to the dotnet/core repo (e.g. `~/git/core`)
- **.NET version** — which version to generate for (e.g. "10.0")
- Optionally, whether to include .NET package availability (requires `PKGS_ORG_TOKEN`)

## Process

### 1. Ensure prerequisites exist

Verify that `release-notes/{version}/os-packages.json` exists in the dotnet/core repo. This file provides the dependency data. If it doesn't exist, inform the user to run the `update-os-packages` skill first.

### 2. Optionally query .NET package feeds

If the user wants to include .NET package availability data, run:

```
dotnet-release query distro-packages --dotnet-version {version} --output release-notes/{version}/distro-packages.json
```

This queries pkgs.org and supplemental feeds to produce a `distro-packages.json` file listing which .NET packages (SDK, runtime, ASP.NET Core) are available in each distro's archive. This step requires `PKGS_ORG_TOKEN`.

If skipped, the generate step will still produce per-distro files with dependencies only (no `dotnet_packages`).

### 3. Generate per-distro files

Run:

```
dotnet-release generate distro-packages {version} {path-to-core}/release-notes
```

This reads `os-packages.json` (required) and `distro-packages.json` (optional) and writes one JSON file per distribution to `release-notes/{version}/distro-packages/`.

Output example (`ubuntu.json`):
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
        { "component": "sdk", "name": "dotnet-sdk-10.0" }
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

### 4. Review results

Verify the output:
- Every distro from `os-packages.json` has a corresponding file in `distro-packages/`
- Dependencies look correct per release
- If enriched, `dotnet_packages` data is present for distros that carry .NET

### 5. Commit

Commit all files in `distro-packages/` with a descriptive message.

## Key facts

- The generate command merges two data sources: `os-packages.json` (dependencies) and optionally `distro-packages.json` (availability from query)
- Without `distro-packages.json`, files contain dependencies only — this is valid and useful
- File names are derived from distro names: lowercase, spaces replaced with `_` (e.g. `azure_linux.json`, `centos_stream.json`)
- Microsoft is phasing out packages.microsoft.com for Ubuntu 24.04+ and newer Fedora — those distros ship .NET in their own archives
- Alpine uses a different naming scheme: `dotnet{major}-{component}` (e.g. `dotnet9-sdk`)
- Package versions in distro feeds may lag behind the latest .NET patch release
