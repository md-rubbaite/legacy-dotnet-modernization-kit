# ADR-0001: Dependency-free static assessment, sanitized pattern rules, wave planning

- Status: accepted
- Date: 2026-11-22

## Context

Teams with .NET Framework estates need a fast answer to three questions before they can budget a
migration: what blocks it, how big is it, and in which order should it happen? They must be able to
run the tool on locked-down build agents, offline, against code that does not build on the agent
(Windows-only references, private feeds), and wire it into CI.

Options:

| Option | Pros | Cons |
|---|---|---|
| Roslyn analyzers / semantic model | Precise symbol resolution | Needs a successful restore and build of a .NET Framework solution on the agent |
| Microsoft's upgrade tooling | Performs conversions, deep analysis | Interactive or IDE-centred; less suited to a CI gate and portfolio reporting |
| **Text and XML rules on sanitized source** | Runs anywhere, no build, no network, milliseconds per project | Pattern matching can miss dynamic usage or match look-alike names |

## Decision

1. Parse project, packages and config files as XML with DTD processing prohibited (no XXE).
2. Strip comments and string/char literals from C# (keeping offsets and line breaks) before
   applying rules, which removes the most common false positives.
3. Judge target frameworks against an embedded support table with an explicit `--as-of` date, so the
   same code produces "supported" in June and "out of support" after 10 November 2026 for net8.0.
4. Plan migration waves by topological order of project references; libraries consumed by .NET
   Framework apps are recommended to multi-target.
5. Report Markdown for people, JSON for dashboards, SARIF for code-scanning platforms. Never echo
   secrets: configuration findings show only the key or connection-string name.

## Consequences

- Good: zero dependencies; one command in CI; deterministic for a given `--as-of`.
- Good: rules and guidance are data, reviewed in docs/rules.md, tested against the LegacyShop fixture.
- Limitation: VB.NET and F# source is not scanned (project files are). Reflection-based or generated
  usage is not seen. Treat the report as a sizing and sequencing tool, and keep running tests.
- Limitation: effort points are relative. Calibrate them with the first project you migrate.
