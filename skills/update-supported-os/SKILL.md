---
name: update-supported-os
description: Audit and update supported-os.json files in dotnet/core to reflect current OS version support. Checks upstream distro lifecycle data to add new releases and retire end-of-life versions.
---

# Update Supported OS

Audit and update `supported-os.json` files in the [dotnet/core](https://github.com/dotnet/core) repository. These files declare which operating system versions are supported for each .NET release.

## When to use

- A new OS version is released (e.g. Ubuntu 26.04, Fedora 44, Alpine 3.23)
- An OS version reaches end-of-life and should be moved to unsupported
- Periodic audit to ensure the support matrix is current

## Prerequisites

Install the `dotnet-release` tool (see [README](../../README.md) for NuGet source setup):

```bash
dotnet tool install -g Dotnet.Release.Tools
```

## Inputs

The user provides:
- **dotnet/core path** — local path to the dotnet/core repo (e.g. `~/git/core`)
- **Versions to audit** — which .NET versions to check (e.g. "8.0+", "10.0 only"), defaults to all active versions
- Optionally, specific distros or OS versions to focus on

## Process

### 1. Identify scope

Determine which .NET versions to audit. For each version, read `release-notes/{version}/supported-os.json`.

### 2. Check upstream lifecycle data

For each Linux distribution in the support matrix:

1. Query [endoflife.date](https://endoflife.date/api/{distro-id}.json) using `web_fetch` to get current lifecycle data
2. Compare against what's listed in supported-os.json
3. Identify:
   - **New releases** — versions that exist upstream but aren't in supported-os.json
   - **EOL versions** — versions listed as supported that have reached end-of-life
   - **Upcoming EOL** — versions approaching end-of-life (informational)

The `id` field in each distribution entry matches the endoflife.date product ID (e.g. `alpine`, `ubuntu`, `debian`, `fedora`, `rhel`).

### 3. Determine .NET version applicability

Not every OS release applies to every .NET version:
- **LTS .NET versions** (8.0, 10.0) support a broader set of OS versions
- **STS .NET versions** (9.0, 11.0) have a shorter support window
- New OS versions are typically only added to .NET versions still in active support
- Check the .NET version's own support status before adding OS versions to it

Present findings to the user with recommendations, grouped by action type.

### 4. Apply changes

On user confirmation:

- **Add new supported versions**: Insert into the `supported-versions` array (keep sorted, newest first)
- **Retire EOL versions**: Move from `supported-versions` to `unsupported-versions`
- **Update `last-updated`**: Set to today's date
- Use the `edit` tool for surgical JSON changes

### 5. Cross-reference with os-packages.json

After updating supported-os.json, check if any newly added distro versions need entries in os-packages.json. If so, inform the user to run the `update-os-packages` skill next.

### 6. Verify and commit

1. Validate all modified JSON files parse correctly (use a file-based app to deserialize with `Dotnet.Release.Support` types)
2. Regenerate the supported-os.md if the `dotnet-release` tool is available:
   ```bash
   dotnet-release generate supported-os {version} {core-path}/release-notes
   ```
3. Show the user a summary of changes
4. On confirmation, commit with a descriptive message

## Key facts

- The `id` field in each distribution matches [endoflife.date](https://endoflife.date) product IDs
- Versions are strings, not numbers — `"3.22"` not `3.22`
- `supported-versions` should be ordered newest-first
- `unsupported-versions` tracks previously-supported versions for historical reference
- Non-Linux OS families (Android, Apple, Windows) follow the same structure but use different lifecycle sources
- The `last-updated` field should reflect the date of any change
