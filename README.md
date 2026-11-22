# legacy-dotnet-modernization-kit

**.NET 8 and .NET 9 went out of support on 10 November 2026. Your .NET Framework estate is older
still. Do you know what blocks the move, how big the job is, and where to start?**

`modernize` scans a repository and reports three things:

- the **blockers** for moving to .NET 10, such as System.Web, Web Forms, WCF hosting, BinaryFormatter, AppDomains and Remoting;
- the **effort and readiness** of every project;
- a **migration plan** in dependency order, with a strangler-fig route for web apps.

It needs no build, no network access and no dependencies, so it runs on any CI agent in seconds.
Output is Markdown for people, JSON for dashboards and SARIF for GitHub code scanning.

[![ci](../../actions/workflows/ci.yml/badge.svg)](../../actions/workflows/ci.yml)
![.NET 10 LTS](https://img.shields.io/badge/.NET-10%20LTS-512BD4)
![SARIF 2.1.0](https://img.shields.io/badge/SARIF-2.1.0-blue)
![License: MIT](https://img.shields.io/badge/license-MIT-green)

## What a report looks like

The sample report below comes from `examples/LegacyShop`, a deliberately old solution: MVC 5,
Web Forms, OWIN, WCF, Remoting, EF6, secrets in `web.config`, plus a worker and a reporting library on now-unsupported
net8.0. The full report is in [docs/sample-report.md](docs/sample-report.md).

| Wave | Project | Kind | Target now | Support | Readiness | Effort | Recommended target |
|---|---|---|---|---|---|---|---|
| 1 | LegacyShop.Core | Library | net461 | out of support since 2022-04-26 | 17/100 | L | net48;net10.0 (multi-target while .NET Framework apps still consume it) |
| 1 | LegacyShop.Reports | Library | net8.0 | out of support since 2026-11-10 | 86/100 | S | net10.0 |
| 2 | LegacyShop.Services | WcfService | net48 | supported (Windows lifecycle) | 26/100 | L | net10.0 with CoreWCF or gRPC |
| 2 | LegacyShop.Web | Web | net472 | supported (Windows lifecycle) | 0/100 | XL | net10.0 (new ASP.NET Core app, migrated route by route) |
| 2 | LegacyShop.Worker | Console | net8.0 | out of support since 2026-11-10 | 45/100 | M | net10.0 |

The plan section of the same report, for the web app:

> **LegacyShop.Web** → net10.0 (new ASP.NET Core app, migrated route by route)
> - Convert the project file to SDK-style. Move packages.config to PackageReference.
> - Stand up an ASP.NET Core (net10.0) app in front of this one with YARP; forward every route to the old app.
> - Share authentication and session with Microsoft.AspNetCore.SystemWebAdapters, then move routes one at a time.
> - Retire the old app when the proxy no longer forwards any route.
> - Move secrets out of config files and rotate them.

## What it checks

There are 31 rules in seven areas. The full list is in [docs/rules.md](docs/rules.md).

| Area | Examples |
|---|---|
| Framework and support | .NET Framework targets; net5.0–net9.0 past end of support (judged on an `--as-of` date); end of support within 12 months; non-SDK project files; packages.config |
| Web | System.Web / MVC 5 / Web API 2, `HttpContext.Current`, Web Forms and ASMX, Global.asax, OWIN, forms auth, HTTP modules, session state |
| Services | WCF hosting and configuration, .NET Remoting, COM+, `WebClient` / `HttpWebRequest` |
| Runtime | BinaryFormatter and other unsafe serializers, `AppDomain.CreateDomain`, `Thread.Abort`, Code Access Security, System.Drawing on Linux, Registry |
| Data | System.Data.SqlClient, Entity Framework 6 |
| Security | MD5/SHA-1/DES/RC2, `RNGCryptoServiceProvider`, plaintext passwords and API keys in config (reported by name only, never by value), machineKey |
| Packages | Packages tied to .NET Framework or deprecated, each with its replacement |

Precision safeguards:

- Comments and string literals are stripped before matching, so a comment that mentions `HttpContext.Current` is not a finding.
- `bin`, `obj`, `packages` and `node_modules` are skipped.
- Placeholders such as `__TOKEN__`, `#{Secret}` and `$(Var)` are not reported as secrets.
- XML with a DTD is refused rather than expanded (no XXE).
- Symbolic links are not followed.

## Use it

```bash
dotnet build src/Modernize.Cli -c Release
dotnet src/Modernize.Cli/bin/Release/net10.0/Modernize.Cli.dll scan /path/to/repo                 # Markdown to stdout
dotnet src/Modernize.Cli/bin/Release/net10.0/Modernize.Cli.dll scan /path/to/repo --format sarif --out modernize.sarif
dotnet src/Modernize.Cli/bin/Release/net10.0/Modernize.Cli.dll scan /path/to/repo --format json --fail-on high
dotnet src/Modernize.Cli/bin/Release/net10.0/Modernize.Cli.dll rules
```

The CLI is also packable as a .NET tool: `dotnet pack src/Modernize.Cli` installs as `modernize`.

| Option | Default | Meaning |
|---|---|---|
| `--format md\|json\|sarif` | `md` | Report format |
| `--out <file>` | stdout | Where to write the report |
| `--fail-on blocker\|high\|medium\|low\|none` | `blocker` | Exit 1 if any finding is at or above this severity |
| `--as-of yyyy-MM-dd` | today | Date for end-of-support checks; pin it for reproducible reports |

To surface findings in pull requests through GitHub code scanning, copy
[examples/modernize-gate.yml](examples/modernize-gate.yml) into your repository.

## From report to delivery

[docs/playbook.md](docs/playbook.md) turns the report into a delivery plan:

1. Take a baseline.
2. Move libraries first, multi-targeting `net48;net10.0`.
3. Move workers and WCF (CoreWCF).
4. Migrate web apps incrementally behind YARP with System.Web adapters.
5. Fix the data and security issues the migration uncovers.

The design and its limits are in [ADR-0001](docs/adr-0001-static-assessment-without-roslyn.md).

## Limits

- The tool scans C# source. For VB.NET and F#, it reads project and config files only.
- Pattern rules cannot see reflection or code generated at build time. Treat the report as sizing and sequencing input, and keep running your tests.
- Effort points are relative. Calibrate them against the first project you migrate.
- Support dates come from Microsoft's published policy, checked on 22 November 2026 (`SupportPolicy.cs`).

## Tests

```bash
dotnet test
```

There are 24 tests. They check the expected findings for every fixture file and that comments,
strings and placeholders are ignored. They check secret redaction, support dates around 10 November
2026, project classification, wave order and recommendations. They also cover SARIF and JSON
consistency, CLI exit codes, DTD refusal, excluded folders and reference cycles. The suite was
checked by mutation: disabling the comment stripper or allowing DTDs makes it fail.

## Author

Built by **Rubbaite T. Hassain**, Software Architect and founder of
[Datazo InfoTech](https://www.datazoinfotech.com.bd). We run .NET modernization programmes for
fintech and healthcare: assessment, strangler-fig delivery and cut-over. Get in touch if you want
this applied to your estate.

MIT licensed.
