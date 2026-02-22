---
name: update-os-packages
description: Audit and update os-packages.json files in dotnet/core to ensure Linux distribution package names are correct. Verifies package names against upstream package repositories and fixes mismatches.
---

# Update OS Packages

Audit and update `os-packages.json` files in the [dotnet/core](https://github.com/dotnet/core) repository. These files declare the Linux distribution packages required to run .NET on each supported distro release.

## When to use

- A new distro version is released (e.g. Ubuntu 26.04, Alpine 3.23) and packages need to be added or verified
- Package names may have changed upstream (e.g. `libicu76` → `libicu78`)
- Periodic audit to ensure all listed package names are still correct

## Prerequisites

Install the `dotnet-release` tool (see [README](../../README.md) for NuGet source setup):

```bash
dotnet tool install -g Dotnet.Release.Tools
```

## Inputs

The user provides:
- **dotnet/core path** — local path to the dotnet/core repo (e.g. `~/git/core`)
- **Versions to audit** — which .NET versions to check (e.g. "8.0+", "10.0 only"), defaults to all active versions

## Process

### 1. Identify scope

Determine which .NET versions to audit. Active versions are those with `os-packages.json` files in the `release-notes/` directory. Skip versions the user excludes.

For each version, read `release-notes/{version}/os-packages.json`.

### 2. Verify package names

Run the `dotnet-release verify os-packages` command for each version. The tool checks each package name against upstream distro package feeds and reports mismatches.

### 3. Cross-reference with supported-os.json

For each version, read `release-notes/{version}/supported-os.json` and check:
- Every Linux distro+version in `supported-os.json` has a matching entry in `os-packages.json`
- No distro+version in `os-packages.json` is absent from `supported-os.json` (stale entries)

Report gaps to the user. **Do not add new distro entries automatically** — the user decides whether to add them.

### 4. Apply fixes

For confirmed mismatches (wrong package names):
- Update the package name in os-packages.json across **all** affected .NET versions (the same distro release uses the same packages regardless of .NET version)
- Use the `edit` tool to make surgical changes

### 5. Verify and commit

1. Validate all modified JSON files parse correctly (use a file-based app to deserialize with `Dotnet.Release.Support` types)
2. Show the user a summary of changes
3. On confirmation, commit with a descriptive message

## Key facts

- Package names like `libicu` are versioned on Debian/Ubuntu (e.g. `libicu76`) but **not** on Fedora/RHEL/SUSE (just `libicu`)
- OpenSSL on Debian 13+ and Ubuntu 24.04+ uses the `t64` suffix (`libssl3t64`)
- Alpine uses different package names entirely (`icu-libs`, `libssl3`, `krb5`)
- The same distro release always has the same packages regardless of .NET version — fixes should be applied across all os-packages.json files
