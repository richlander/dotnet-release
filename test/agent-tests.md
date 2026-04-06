# Agent Tests

Validation scenarios for agent-driven skills. Each test defines a workflow an AI agent should be able to execute end-to-end, with clear success criteria.

## Test 1: Update Supported OS

**Skill:** `update-supported-os`
**Tool:** `release-notes-gen verify supported-os`

An agent audits the .NET supported OS matrix against upstream lifecycle data and, if issues are found, prepares a PR with fixes.

### Flow

```text
verify → early-out if clean → update JSON → regenerate markdown → PR
```

### Steps

1. **Verify** — Run `release-notes-gen verify supported-os <version>` for each active .NET version. The report categorizes issues as:
   - ⚠️ WARNING: EOL but still listed as supported — move to `unsupported-versions`
   - ❗ IMPORTANT: Active releases not listed — consider adding to `supported-versions`
   - 💡 TIP: Active but listed as unsupported — verify intentional
   - 🔥 CAUTION: Approaching EOL within 3 months — informational
2. **Early out** — If the report has no issues, stop. Nothing to do.
3. **Update** — For each actionable finding, edit `supported-os.json` in `dotnet/core`:
   - Move EOL versions from `supported-versions` to `unsupported-versions`
   - Add missing active versions to `supported-versions` (sorted newest-first)
   - Update `last-updated` to today's date
4. **Regenerate** — Run `release-notes-gen generate supported-os <version> <core-path>/release-notes` to regenerate the markdown.
5. **PR** — Create a branch, commit changes, and open a PR against `dotnet/core`.

### Success criteria

- Agent correctly interprets verify output to decide whether work is needed.
- JSON edits are valid and follow existing structure (versions as strings, sorted newest-first).
- Regenerated markdown reflects the JSON changes.
- PR has a descriptive title and body summarizing what changed and why.

### Exit codes

| Code | Meaning |
|------|---------|
| 0 | No issues found — nothing to do |
| 2 | Issues found — report written to stdout |

### Skill reference

See [`skills/update-supported-os/SKILL.md`](../skills/update-supported-os/SKILL.md) for the full agent instructions and [`references/verify-output-example.md`](../skills/update-supported-os/references/verify-output-example.md) for example verify output.
