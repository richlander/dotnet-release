---
name: update-distro-packages
description: Generate and update distro-packages.json files documenting which .NET packages are available in each Linux distribution's native archive and the Microsoft feed, for a given .NET version.
---

# Update Distro Packages

Generate and update `distro-packages.json` files that document which .NET packages (SDK, runtime, ASP.NET Core runtime, etc.) are available in each Linux distribution, across multiple package feeds.

## When to use

- A new .NET version is released and you need to document which distros carry it
- A distro ships or drops a .NET version in their archive
- Periodic audit to keep the availability data current
- You want to understand where users can get .NET packages for a specific distro+version combination

## Background

.NET packages come from multiple sources per distribution:

- **Built-in feed** — the distro's own archive (e.g. `apt install dotnet-sdk-9.0` on Ubuntu)
- **Backports feed** — newer versions backported to LTS distros (Ubuntu-specific)
- **Microsoft feed** — packages.microsoft.com, available for many distros but being phased out for newer Ubuntu/Fedora
- **Community feed** — community-maintained packages (Alpine `community` repo)

Package naming varies by distro:
- Debian/Ubuntu/Fedora: `dotnet-sdk-9.0`, `dotnet-runtime-9.0`, `aspnetcore-runtime-9.0`
- Alpine: `dotnet9-sdk`, `dotnet9-runtime`

## Prerequisites

Install the `dotnet-release` tool (see [README](../../README.md) for NuGet source setup):

```bash
dotnet tool install -g Dotnet.Release.Tools
```

## Inputs

The user provides:
- **dotnet/core path** — local path to the dotnet/core repo (e.g. `~/git/core`)
- **.NET version** — which version to audit (e.g. "9.0")
- Optionally, specific distros to focus on

## Process

### 1. Determine scope

Read `release-notes/{version}/supported-os.json` to get the list of supported Linux distributions and versions.

### 2. Query package feeds

Run `dotnet-release query distro-packages {version}` to check each feed. The tool uses the feed URLs from the embedded `distro-sources.json` to:

1. For each distro+version, check the built-in/community feed for .NET SDK, runtime, and ASP.NET Core runtime packages
2. Check the Microsoft feed (packages.microsoft.com) where applicable
3. For Ubuntu, also check the backports feed
4. Collect package names, versions, and architectures

### 3. Review results

Present the findings to the user as a summary table showing:
- Which .NET components are available per distro+version
- Which feed(s) they come from
- Any gaps (supported distro but no packages available)

### 4. Generate output

Write `distro-packages.json` to `release-notes/{version}/` in the dotnet/core repo. The file follows the `DistroPackagesOverview` schema with per-feed package listings.

### 5. Cross-reference with docs

Compare results against the install documentation in `dotnet/docs` (e.g. `docs/core/install/linux-ubuntu-install.md`) which maintains availability tables per distro. Flag any discrepancies between what the feeds actually contain and what the docs claim.

### 6. Verify and commit

1. Validate the generated JSON parses correctly
2. Show the user a summary of the generated file
3. On confirmation, commit with a descriptive message

## Key facts

- Microsoft is phasing out packages.microsoft.com for Ubuntu 24.04+ and newer Fedora — those distros ship .NET in their own archives
- Ubuntu 22.04 is the last Ubuntu version with Microsoft feed packages
- Alpine packages are in the `community` repo, not `main`
- Alpine uses a different naming scheme: `dotnet{major}-{component}` (e.g. `dotnet9-sdk`)
- The `dotnet/docs` repo (`~/git/docs`) has authoritative availability tables in `docs/core/install/linux-*.md`
- Package versions in distro feeds may lag behind the latest .NET patch release
