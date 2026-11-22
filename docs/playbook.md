# Modernization playbook: .NET Framework and out-of-support .NET to .NET 10

This playbook turns the scanner's report into a delivery plan. It assumes a production system that
cannot stop for a rewrite.

## 0. Baseline (week 1)

1. Run `modernize scan` on the whole repository; keep the JSON report as the baseline.
2. Record what the report cannot see: deployment topology, Windows-only infrastructure (IIS
   modules, MSMQ, COM components, GAC assemblies), scheduled tasks, and who owns each project.
3. Put characterization tests around the riskiest flows: checkout, billing, authentication.
   A migration without tests is a rewrite with extra steps.
4. Add the scan to CI with `--fail-on blocker` on new code only, using the SARIF upload (see
   `examples/modernize-gate.yml`), so no new blockers arrive while you remove old ones.

## 1. Libraries first (wave 1)

Leaf libraries have no project dependencies, so they move first:

1. Convert to SDK-style; switch to PackageReference.
2. Remove blockers (`BinaryFormatter`, `AppDomain`, Remoting) inside the library.
3. **Multi-target** `net48;net10.0` while .NET Framework applications still reference the library.
   Both worlds build from one codebase; `#if NETFRAMEWORK` stays rare and temporary.
4. Release the package; consumers upgrade at their own pace.

## 2. Services and workers

- **Console apps, workers, Windows services:** retarget to net10.0 with the Generic Host.
  On Linux, run under systemd or in containers; on Windows, use `UseWindowsService`.
- **WCF services:** port contracts to CoreWCF, which keeps existing SOAP clients working. Expose gRPC
  or REST for new clients. Retire SOAP when the last client moves.
- **Out-of-support .NET (net5.0–net9.0):** usually a retarget plus breaking-change review. Do these
  early; they are cheap and remove unpatched runtimes from production.

## 3. Web applications: strangler fig

Do not freeze features for a big-bang rewrite. Grow the new application around the old one:

```mermaid
flowchart LR
    U[Users] --> Y[ASP.NET Core app on net10.0<br/>with YARP]
    Y -- migrated routes --> N[New endpoints<br/>Razor Pages / API]
    Y -- everything else --> L[Legacy ASP.NET app<br/>on .NET Framework]
    N <-. shared auth and session<br/>System.Web adapters .-> L
```

1. Deploy an ASP.NET Core app that proxies **every** request to the legacy app with YARP. Nothing
   changes for users; you have proved the hosting, networking and observability.
2. Share authentication and session between the two apps with `Microsoft.AspNetCore.SystemWebAdapters`.
   Share Data Protection keys (see finding LDM142).
3. Move routes one at a time. Start with low-risk, high-traffic read endpoints, then forms, then the
   hardest flows. Each move ships independently behind a feature flag and can be rolled back in the
   proxy.
4. Replace `HttpContext.Current` with explicit dependencies as code moves; replace HTTP modules with
   middleware; move `Global.asax` logic to `Program.cs`.
5. Web Forms pages have no port: rebuild them (Razor Pages or Blazor) in priority order.
6. When the proxy forwards nothing, switch off the legacy app.

## 4. Data and security work that rides along

- `System.Data.SqlClient` → `Microsoft.Data.SqlClient` (watch the `Encrypt=true` default).
- EF6 runs on .NET; move to EF Core as a separate, measurable project.
- Secrets found in config files: move to a secret store **and rotate them**. They are in your git history.
- Weak cryptography: replace hashes used for security, and plan re-hashing of stored passwords at
  next login.
- `BinaryFormatter` data at rest: write a one-off converter before the runtime switch, not after.

## 5. Measure

Re-run the scan every sprint. Track blocker count, readiness per project and the share of traffic
served by net10.0 behind the proxy. Those three numbers are the progress report for management.
