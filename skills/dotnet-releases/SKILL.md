---
name: dotnet-releases
description: Answer .NET release lifecycle, timeline, patch, and CVE questions by driving the public `dotnet-release` CLI over the release-index graph.
---

# dotnet-releases

Use this skill when a user asks about .NET release status, support lifecycle, what shipped in a given month or day, or which CVEs affected a product or package.

## When to use

- "What .NET versions are currently supported?"
- "What shipped in March 2026?"
- "What happened on 2026-03-10?"
- "Which CVEs affected the runtime since 2025?"
- "Which package was fixed for a recent .NET security update?"
- "What is the latest LTS or latest security patch?"

## Tool to use

Prefer the public query tool:

```bash
dotnet-release ...
```

If the tool is not installed globally but you are working in this repo, run it from source:

```bash
dotnet run --project src/Dotnet.Release.Tool -- ...
```

The tool is currently pinned to the `release-index` branch of `dotnet/core`.

## Query pattern

Start broad, then drill down:

1. **Overview / support status**
   ```bash
   dotnet-release
   dotnet-release releases
   dotnet-release releases --all
   ```

2. **Specific major version**
   ```bash
   dotnet-release release 10.0
   ```

3. **Chronological traversal**
   ```bash
   dotnet-release timeline
   dotnet-release timeline 2026
   dotnet-release timeline 2026-03
   dotnet-release timeline 2026-03-10
   ```

4. **Security / CVE questions**
   ```bash
   dotnet-release cves -n 6
   dotnet-release cves since 2025
   dotnet-release cves since 2026-01 --product runtime
   dotnet-release cves --package System.Security.Cryptography.Cose since 2026-01
   ```

## Answering guidance

- Prefer the tool over hand-reading raw JSON when the question fits the CLI surface.
- Use **timeline** for date-based questions and **cves** for security questions.
- Summarize the result in natural language with exact dates and versions.
- Avoid dumping raw command output unless the user asks for it.
- If a question needs package or product scoping, add `--package` or `--product` instead of manually filtering.
- Use `release-notes-gen` only for maintainer workflows; it is not the public query surface.
