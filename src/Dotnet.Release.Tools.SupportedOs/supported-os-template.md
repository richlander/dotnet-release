# .NET {{version}} - Supported OS versions

Last Updated: {{lastUpdated}}; Support phase: {{supportPhase}}

[.NET {{version}}](README.md) is an [{{releaseType}}](../../release-policies.md) release and [is supported](../../support.md) on multiple operating systems per their lifecycle policy.

{{families}}

{{#if libc}}
## Libc

{{libc}}
{{/if}}

{{#if notes}}
## Notes

{{notes}}
{{/if}}

{{#if unsupported}}
## Out of support OS versions

OS versions that are out of support by the OS publisher are not tested or supported by .NET.

{{unsupported}}
{{/if}}
