# Rules

Generated with `modernize rules`. Severity drives the readiness score and the `--fail-on` gate.

| Id | Rule | Severity | Category | Guidance |
|---|---|---|---|---|
| LDM001 | Non-SDK-style project file | Medium | project | Convert to SDK-style: drop the explicit file and assembly lists, keep package references. Required before retargeting; mostly mechanical, and Microsoft's upgrade tooling automates much of it. |
| LDM002 | .NET Framework target | High | framework | Retarget to net10.0 (LTS, supported until 2028-11-14). Shared libraries can multi-target net48;net10.0 during the transition. |
| LDM003 | Target framework out of support | High | framework | No security fixes after end of support. Retarget to net10.0 and review the breaking-change lists of each version skipped. |
| LDM004 | Target framework reaches end of support within 12 months | Medium | framework | Plan the move to net10.0 now so it does not collide with the end-of-support date. |
| LDM005 | packages.config | Low | project | Migrate to PackageReference (Visual Studio: right-click packages.config > Migrate). Transitive dependencies become explicit. |
| LDM101 | System.Web dependency (ASP.NET MVC 5 / Web API 2 / HttpContext.Current) | Blocker | web | System.Web does not exist on .NET. Move controllers to ASP.NET Core; replace HttpContext.Current with IHttpContextAccessor or explicit parameters. For large apps, migrate incrementally (strangler fig, see docs/playbook.md). |
| LDM102 | ASP.NET Web Forms or ASMX | Blocker | web | No Web Forms on .NET. Rewrite pages (Razor Pages or Blazor) and ASMX services (Web API or gRPC) route by route behind a YARP proxy. |
| LDM103 | Global.asax / HttpApplication | High | web | Move application events and filters to Program.cs: middleware, hosted services and exception handlers. |
| LDM104 | OWIN / Katana pipeline | High | web | Port OWIN middleware to ASP.NET Core middleware; Katana authentication maps to ASP.NET Core authentication handlers. |
| LDM110 | WCF service host | Blocker | services | Server-side WCF is not in .NET. Port to CoreWCF (keeps existing SOAP clients working) or expose gRPC/REST for new clients. |
| LDM111 | WCF client proxy | Low | services | Supported on .NET through the System.ServiceModel.* client packages; regenerate the proxy with dotnet-svcutil. |
| LDM112 | .NET Remoting | Blocker | services | Not available on .NET. Replace with gRPC, HTTP APIs or a message broker. |
| LDM113 | COM+ / System.EnterpriseServices | Blocker | services | Not available on .NET. Move transactions to the database or TransactionScope; host components as services. |
| LDM114 | WebClient / WebRequest / HttpWebRequest | Medium | services | Obsolete since .NET 6 (SYSLIB0014). Use HttpClient from IHttpClientFactory, with timeouts and resilience handlers. |
| LDM120 | BinaryFormatter or another unsafe serializer | Blocker | security | BinaryFormatter always throws from .NET 9 and deserializing untrusted data with it allows remote code execution. Use System.Text.Json or a schema-based format; plan a migration for data already persisted. |
| LDM121 | AppDomain creation | Blocker | runtime | AppDomain.CreateDomain throws PlatformNotSupportedException on .NET. Use AssemblyLoadContext for plug-ins or separate processes for isolation. |
| LDM122 | Thread.Abort | High | runtime | Throws PlatformNotSupportedException on .NET. Use cooperative cancellation with CancellationToken. |
| LDM123 | Code Access Security | Medium | runtime | CAS is not enforced on .NET and its APIs are obsolete (SYSLIB0003). Remove the attributes; use OS-level or process isolation for untrusted code. |
| LDM124 | System.Drawing | Medium | portability | System.Drawing.Common is Windows-only since .NET 6. On Linux containers use ImageSharp or SkiaSharp. |
| LDM125 | Windows Registry | Low | portability | Works on Windows only. Move settings to configuration (appsettings, environment, Key Vault) for container hosting. |
| LDM126 | ConfigurationManager | Low | configuration | Works through the System.Configuration.ConfigurationManager package, but IConfiguration with the options pattern supports environments, secrets and reload. |
| LDM130 | System.Data.SqlClient | Medium | data | Deprecated. Switch to Microsoft.Data.SqlClient (note Encrypt=true is the default from version 4). |
| LDM131 | Entity Framework 6 | Medium | data | EF6 (6.3+) runs on .NET, so it need not block the move. Plan EF Core separately: lazy loading, EDMX models and some LINQ translations differ. |
| LDM140 | Weak or obsolete cryptography | High | security | MD5, SHA-1, DES, TripleDES and RC2 are unsuitable for security purposes; RNGCryptoServiceProvider is obsolete (SYSLIB0023). Use SHA-256+, AES-GCM, PBKDF2/Argon2 for passwords and RandomNumberGenerator. |
| LDM141 | Secret in a configuration file | High | security | Move credentials to a secret store (Azure Key Vault, AWS Secrets Manager, user-secrets for development), prefer managed identities, and rotate the exposed secret. |
| LDM142 | machineKey in configuration | Medium | security | ASP.NET Core uses Data Protection keys. Share keys between old and new apps during a strangler migration (System.Web adapters) and keep keys out of source control. |
| LDM150 | Forms or Windows authentication in web.config | High | web | Re-implement with ASP.NET Core cookie, OpenID Connect or Negotiate authentication; share the auth ticket during incremental migration. |
| LDM151 | HTTP modules or handlers | Medium | web | Port IHttpModule/IHttpHandler to middleware or endpoints. |
| LDM152 | Session state | Medium | web | ASP.NET Core session needs IDistributedCache (Redis, SQL Server) and is not locked per request; review concurrent access. |
| LDM153 | system.serviceModel configuration | High | services | Bindings and behaviours move to code (CoreWCF) or are replaced with gRPC/REST configuration. |
| LDM160 | Package tied to .NET Framework or deprecated | High | packages | Replace with the package named in the finding. |
