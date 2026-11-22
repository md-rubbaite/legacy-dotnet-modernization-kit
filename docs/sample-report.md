# .NET modernization assessment

- Scanned: `LegacyShop`, 6 projects, 19 files
- As of: 2026-11-22 (support dates checked 2026-11-22; current LTS net10.0, supported until 2028-11-14)
- Findings: 12 blocker, 25 high, 10 medium, 3 low

## Projects and migration order

| Wave | Project | Kind | Target now | Support | Readiness | Effort | Recommended target |
|---|---|---|---|---|---|---|---|
| 1 | LegacyShop.Core | Library | net461 | out of support since 2022-04-26 | 17/100 | L (40 pts) | net48;net10.0 (multi-target while .NET Framework apps still consume it) |
| 1 | LegacyShop.Reports | Library | net8.0 | out of support since 2026-11-10 | 86/100 | S (6 pts) | net10.0 |
| 2 | LegacyShop.Core.Tests | Test | net48 | supported (follows the Windows lifecycle) | 90/100 | S (8 pts) | net10.0 |
| 2 | LegacyShop.Services | WcfService | net48 | supported (follows the Windows lifecycle) | 26/100 | L (40 pts) | net10.0 with CoreWCF or gRPC |
| 2 | LegacyShop.Web | Web | net472 | supported (follows the Windows lifecycle) | 0/100 | XL (73 pts) | net10.0 (new ASP.NET Core app, migrated route by route) |
| 2 | LegacyShop.Worker | Console | net8.0 | out of support since 2026-11-10 | 45/100 | M (17 pts) | net10.0 |

Readiness: 100 minus 25 per blocker rule, 10 per high, 4 per medium, 1 per low. Effort points are relative, for sequencing and sizing; calibrate them against your first migrated project.

## Plan

### Wave 1

- **LegacyShop.Core** → net48;net10.0 (multi-target while .NET Framework apps still consume it)
  - Convert the project file to SDK-style.
  - Move packages.config to PackageReference.
  - Remove blocker LDM120 (BinaryFormatter or another unsafe serializer).
  - Multi-target net48;net10.0 so .NET Framework consumers keep building while modern ones move.
- **LegacyShop.Reports** → net10.0
  - Retarget to net10.0; run the tests; review breaking changes of every skipped version.

### Wave 2

- **LegacyShop.Core.Tests** → net10.0
  - Retarget to net10.0; run the tests; review breaking changes of every skipped version.
- **LegacyShop.Services** → net10.0 with CoreWCF or gRPC
  - Convert the project file to SDK-style.
  - Port service contracts to CoreWCF so existing SOAP clients keep working; offer gRPC or REST to new clients.
  - Remove blocker LDM112 (.NET Remoting).
- **LegacyShop.Web** → net10.0 (new ASP.NET Core app, migrated route by route)
  - Convert the project file to SDK-style.
  - Move packages.config to PackageReference.
  - Stand up an ASP.NET Core (net10.0) app in front of this one with YARP; forward every route to the old app.
  - Share authentication and session with Microsoft.AspNetCore.SystemWebAdapters, then move routes one at a time, highest value or lowest risk first.
  - Retire the old app when the proxy no longer forwards any route.
  - Move secrets out of config files and rotate them.
- **LegacyShop.Worker** → net10.0
  - Remove blocker LDM121 (AppDomain creation).
  - Retarget to net10.0; run the tests; review breaking changes of every skipped version.

## Findings

### LDM101 System.Web dependency (ASP.NET MVC 5 / Web API 2 / HttpContext.Current) (Blocker, 5)

System.Web does not exist on .NET. Move controllers to ASP.NET Core; replace HttpContext.Current with IHttpContextAccessor or explicit parameters. For large apps, migrate incrementally (strangler fig, see docs/playbook.md).

- `LegacyShop.Web/Controllers/OrdersController.cs:1` using System.Web;
- `LegacyShop.Web/Controllers/OrdersController.cs:2` using System.Web.Mvc;
- `LegacyShop.Web/Controllers/OrdersController.cs:10` var user = HttpContext.Current.User.Identity.Name;
- `LegacyShop.Web/Global.asax.cs:1` using System.Web.Mvc;
- `LegacyShop.Web/Global.asax.cs:2` using System.Web.Routing;

### LDM102 ASP.NET Web Forms or ASMX (Blocker, 1)

No Web Forms on .NET. Rewrite pages (Razor Pages or Blazor) and ASMX services (Web API or gRPC) route by route behind a YARP proxy.

- `LegacyShop.Web/Checkout.aspx:1` Checkout.aspx

### LDM110 WCF service host (Blocker, 2)

Server-side WCF is not in .NET. Port to CoreWCF (keeps existing SOAP clients working) or expose gRPC/REST for new clients.

- `LegacyShop.Services/IOrderService.cs:6` [ServiceContract]
- `LegacyShop.Services/OrderService.svc:1` OrderService.svc

### LDM112 .NET Remoting (Blocker, 2)

Not available on .NET. Replace with gRPC, HTTP APIs or a message broker.

- `LegacyShop.Services/IOrderService.cs:1` using System.Runtime.Remoting;
- `LegacyShop.Services/IOrderService.cs:20` public static void Configure() => RemotingConfiguration.Configure("bridge.config", false);

### LDM120 BinaryFormatter or another unsafe serializer (Blocker, 1)

BinaryFormatter always throws from .NET 9 and deserializing untrusted data with it allows remote code execution. Use System.Text.Json or a schema-based format; plan a migration for data already persisted.

- `LegacyShop.Core/Catalog.cs:29` var formatter = new BinaryFormatter();

### LDM121 AppDomain creation (Blocker, 1)

AppDomain.CreateDomain throws PlatformNotSupportedException on .NET. Use AssemblyLoadContext for plug-ins or separate processes for isolation.

- `LegacyShop.Worker/Program.cs:9` var sandbox = AppDomain.CreateDomain("plugins");

### LDM002 .NET Framework target (High, 4)

Retarget to net10.0 (LTS, supported until 2028-11-14). Shared libraries can multi-target net48;net10.0 during the transition.

- `LegacyShop.Core.Tests/LegacyShop.Core.Tests.csproj:1` net48: supported (follows the Windows lifecycle)
- `LegacyShop.Core/LegacyShop.Core.csproj:1` net461: out of support since 2022-04-26
- `LegacyShop.Services/LegacyShop.Services.csproj:1` net48: supported (follows the Windows lifecycle)
- `LegacyShop.Web/LegacyShop.Web.csproj:1` net472: supported (follows the Windows lifecycle)

### LDM003 Target framework out of support (High, 3)

No security fixes after end of support. Retarget to net10.0 and review the breaking-change lists of each version skipped.

- `LegacyShop.Core/LegacyShop.Core.csproj:1` net461: out of support since 2022-04-26
- `LegacyShop.Reports/LegacyShop.Reports.csproj:1` net8.0: out of support since 2026-11-10
- `LegacyShop.Worker/LegacyShop.Worker.csproj:1` net8.0: out of support since 2026-11-10

### LDM103 Global.asax / HttpApplication (High, 2)

Move application events and filters to Program.cs: middleware, hosted services and exception handlers.

- `LegacyShop.Web/Global.asax:1` Global.asax
- `LegacyShop.Web/Global.asax.cs:6` public class MvcApplication : System.Web.HttpApplication

### LDM104 OWIN / Katana pipeline (High, 4)

Port OWIN middleware to ASP.NET Core middleware; Katana authentication maps to ASP.NET Core authentication handlers.

- `LegacyShop.Web/Startup.cs:1` using Microsoft.Owin;
- `LegacyShop.Web/Startup.cs:2` using Owin;
- `LegacyShop.Web/Startup.cs:4` [assembly: OwinStartup(typeof(LegacyShop.Web.Startup))]
- `LegacyShop.Web/Startup.cs:10` public void Configuration(IAppBuilder app) { }

### LDM122 Thread.Abort (High, 1)

Throws PlatformNotSupportedException on .NET. Use cooperative cancellation with CancellationToken.

- `LegacyShop.Worker/Program.cs:7` workerThread.Abort();

### LDM140 Weak or obsolete cryptography (High, 2)

MD5, SHA-1, DES, TripleDES and RC2 are unsuitable for security purposes; RNGCryptoServiceProvider is obsolete (SYSLIB0023). Use SHA-256+, AES-GCM, PBKDF2/Argon2 for passwords and RandomNumberGenerator.

- `LegacyShop.Core/Catalog.cs:39` using (var md5 = MD5.Create())
- `LegacyShop.Worker/Program.cs:10` using var rng = new RNGCryptoServiceProvider();

### LDM141 Secret in a configuration file (High, 2)

Move credentials to a secret store (Azure Key Vault, AWS Secrets Manager, user-secrets for development), prefer managed identities, and rotate the exposed secret.

- `LegacyShop.Web/web.config:4` connection string "Shop" contains a password (redacted)
- `LegacyShop.Web/web.config:10` appSetting "PaymentApiKey" holds a secret (redacted)

### LDM150 Forms or Windows authentication in web.config (High, 1)

Re-implement with ASP.NET Core cookie, OpenID Connect or Negotiate authentication; share the auth ticket during incremental migration.

- `LegacyShop.Web/web.config:15` &lt;authentication mode="Forms">

### LDM153 system.serviceModel configuration (High, 1)

Bindings and behaviours move to code (CoreWCF) or are replaced with gRPC/REST configuration.

- `LegacyShop.Services/web.config:3` &lt;system.serviceModel>

### LDM160 Package tied to .NET Framework or deprecated (High, 5)

Replace with the package named in the finding.

- `LegacyShop.Core/LegacyShop.Core.csproj:1` System.Data.SqlClient 4.8.6 → Microsoft.Data.SqlClient
- `LegacyShop.Web/LegacyShop.Web.csproj:1` Microsoft.AspNet.Mvc 5.2.9 → ASP.NET Core (built in)
- `LegacyShop.Web/LegacyShop.Web.csproj:1` Microsoft.AspNet.WebApi.Core 5.2.9 → ASP.NET Core (built in)
- `LegacyShop.Web/LegacyShop.Web.csproj:1` Microsoft.Owin.Security.Cookies 4.2.2 → ASP.NET Core middleware
- `LegacyShop.Web/LegacyShop.Web.csproj:1` WindowsAzure.Storage 9.3.3 → Azure.Storage.Blobs / Azure.Storage.Queues / Azure.Data.Tables

### LDM001 Non-SDK-style project file (Medium, 3)

Convert to SDK-style: drop the explicit file and assembly lists, keep package references. Required before retargeting; mostly mechanical, and Microsoft's upgrade tooling automates much of it.

- `LegacyShop.Core/LegacyShop.Core.csproj:1` &lt;Project ToolsVersion=…> without Sdk
- `LegacyShop.Services/LegacyShop.Services.csproj:1` &lt;Project ToolsVersion=…> without Sdk
- `LegacyShop.Web/LegacyShop.Web.csproj:1` &lt;Project ToolsVersion=…> without Sdk

### LDM114 WebClient / WebRequest / HttpWebRequest (Medium, 1)

Obsolete since .NET 6 (SYSLIB0014). Use HttpClient from IHttpClientFactory, with timeouts and resilience handlers.

- `LegacyShop.Core/Pricing.cs:11` using (var client = new WebClient())

### LDM124 System.Drawing (Medium, 1)

System.Drawing.Common is Windows-only since .NET 6. On Linux containers use ImageSharp or SkiaSharp.

- `LegacyShop.Reports/Chart.cs:1` using System.Drawing;

### LDM130 System.Data.SqlClient (Medium, 1)

Deprecated. Switch to Microsoft.Data.SqlClient (note Encrypt=true is the default from version 4).

- `LegacyShop.Core/Catalog.cs:3` using System.Data.SqlClient;

### LDM131 Entity Framework 6 (Medium, 1)

EF6 (6.3+) runs on .NET, so it need not block the move. Plan EF Core separately: lazy loading, EDMX models and some LINQ translations differ.

- `LegacyShop.Core/Catalog.cs:2` using System.Data.Entity;

### LDM142 machineKey in configuration (Medium, 1)

ASP.NET Core uses Data Protection keys. Share keys between old and new apps during a strangler migration (System.Web adapters) and keep keys out of source control.

- `LegacyShop.Web/web.config:19` &lt;machineKey …> (values redacted)

### LDM151 HTTP modules or handlers (Medium, 1)

Port IHttpModule/IHttpHandler to middleware or endpoints.

- `LegacyShop.Web/web.config:20` &lt;httpModules>

### LDM152 Session state (Medium, 1)

ASP.NET Core session needs IDistributedCache (Redis, SQL Server) and is not locked per request; review concurrent access.

- `LegacyShop.Web/web.config:18` &lt;sessionState mode="InProc">

### LDM005 packages.config (Low, 2)

Migrate to PackageReference (Visual Studio: right-click packages.config > Migrate). Transitive dependencies become explicit.

- `LegacyShop.Core/packages.config:1` packages.config
- `LegacyShop.Web/packages.config:1` packages.config

### LDM126 ConfigurationManager (Low, 1)

Works through the System.Configuration.ConfigurationManager package, but IConfiguration with the options pattern supports environments, secrets and reload.

- `LegacyShop.Core/Pricing.cs:10` var url = ConfigurationManager.AppSettings["RatesUrl"];

_Generated by legacy-dotnet-modernization-kit. Static analysis finds patterns, not every behaviour change; run your tests on the new target._
