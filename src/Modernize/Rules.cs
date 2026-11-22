using System.Text.RegularExpressions;

namespace Modernize;

/// <summary>The rule catalogue. Guidance names the replacement, not just the problem.</summary>
public static class Rules
{
    // Project and framework
    public static readonly Rule LegacyProjectFormat = new("LDM001", "Non-SDK-style project file", Severity.Medium, "project", 3,
        "Convert to SDK-style: drop the explicit file and assembly lists, keep package references. Required before retargeting; mostly mechanical, and Microsoft's upgrade tooling automates much of it.");
    public static readonly Rule NetFrameworkTarget = new("LDM002", ".NET Framework target", Severity.High, "framework", 8,
        "Retarget to net10.0 (LTS, supported until 2028-11-14). Shared libraries can multi-target net48;net10.0 during the transition.");
    public static readonly Rule UnsupportedFramework = new("LDM003", "Target framework out of support", Severity.High, "framework", 3,
        "No security fixes after end of support. Retarget to net10.0 and review the breaking-change lists of each version skipped.");
    public static readonly Rule FrameworkEndingSoon = new("LDM004", "Target framework reaches end of support within 12 months", Severity.Medium, "framework", 3,
        "Plan the move to net10.0 now so it does not collide with the end-of-support date.");
    public static readonly Rule PackagesConfig = new("LDM005", "packages.config", Severity.Low, "project", 1,
        "Migrate to PackageReference (Visual Studio: right-click packages.config > Migrate). Transitive dependencies become explicit.");

    // Web stack
    public static readonly Rule SystemWeb = new("LDM101", "System.Web dependency (ASP.NET MVC 5 / Web API 2 / HttpContext.Current)", Severity.Blocker, "web", 13,
        "System.Web does not exist on .NET. Move controllers to ASP.NET Core; replace HttpContext.Current with IHttpContextAccessor or explicit parameters. For large apps, migrate incrementally (strangler fig, see docs/playbook.md).");
    public static readonly Rule WebForms = new("LDM102", "ASP.NET Web Forms or ASMX", Severity.Blocker, "web", 21,
        "No Web Forms on .NET. Rewrite pages (Razor Pages or Blazor) and ASMX services (Web API or gRPC) route by route behind a YARP proxy.");
    public static readonly Rule GlobalAsax = new("LDM103", "Global.asax / HttpApplication", Severity.High, "web", 3,
        "Move application events and filters to Program.cs: middleware, hosted services and exception handlers.");
    public static readonly Rule OwinKatana = new("LDM104", "OWIN / Katana pipeline", Severity.High, "web", 5,
        "Port OWIN middleware to ASP.NET Core middleware; Katana authentication maps to ASP.NET Core authentication handlers.");

    // Remote calls and services
    public static readonly Rule WcfService = new("LDM110", "WCF service host", Severity.Blocker, "services", 13,
        "Server-side WCF is not in .NET. Port to CoreWCF (keeps existing SOAP clients working) or expose gRPC/REST for new clients.");
    public static readonly Rule WcfClient = new("LDM111", "WCF client proxy", Severity.Low, "services", 2,
        "Supported on .NET through the System.ServiceModel.* client packages; regenerate the proxy with dotnet-svcutil.");
    public static readonly Rule Remoting = new("LDM112", ".NET Remoting", Severity.Blocker, "services", 13,
        "Not available on .NET. Replace with gRPC, HTTP APIs or a message broker.");
    public static readonly Rule EnterpriseServices = new("LDM113", "COM+ / System.EnterpriseServices", Severity.Blocker, "services", 13,
        "Not available on .NET. Move transactions to the database or TransactionScope; host components as services.");
    public static readonly Rule LegacyHttpClient = new("LDM114", "WebClient / WebRequest / HttpWebRequest", Severity.Medium, "services", 2,
        "Obsolete since .NET 6 (SYSLIB0014). Use HttpClient from IHttpClientFactory, with timeouts and resilience handlers.");

    // Runtime
    public static readonly Rule UnsafeSerializer = new("LDM120", "BinaryFormatter or another unsafe serializer", Severity.Blocker, "security", 8,
        "BinaryFormatter always throws from .NET 9 and deserializing untrusted data with it allows remote code execution. Use System.Text.Json or a schema-based format; plan a migration for data already persisted.");
    public static readonly Rule AppDomains = new("LDM121", "AppDomain creation", Severity.Blocker, "runtime", 8,
        "AppDomain.CreateDomain throws PlatformNotSupportedException on .NET. Use AssemblyLoadContext for plug-ins or separate processes for isolation.");
    public static readonly Rule ThreadAbort = new("LDM122", "Thread.Abort", Severity.High, "runtime", 3,
        "Throws PlatformNotSupportedException on .NET. Use cooperative cancellation with CancellationToken.");
    public static readonly Rule CodeAccessSecurity = new("LDM123", "Code Access Security", Severity.Medium, "runtime", 2,
        "CAS is not enforced on .NET and its APIs are obsolete (SYSLIB0003). Remove the attributes; use OS-level or process isolation for untrusted code.");
    public static readonly Rule SystemDrawing = new("LDM124", "System.Drawing", Severity.Medium, "portability", 3,
        "System.Drawing.Common is Windows-only since .NET 6. On Linux containers use ImageSharp or SkiaSharp.");
    public static readonly Rule Registry = new("LDM125", "Windows Registry", Severity.Low, "portability", 2,
        "Works on Windows only. Move settings to configuration (appsettings, environment, Key Vault) for container hosting.");
    public static readonly Rule ConfigurationManager = new("LDM126", "ConfigurationManager", Severity.Low, "configuration", 2,
        "Works through the System.Configuration.ConfigurationManager package, but IConfiguration with the options pattern supports environments, secrets and reload.");

    // Data
    public static readonly Rule LegacySqlClient = new("LDM130", "System.Data.SqlClient", Severity.Medium, "data", 2,
        "Deprecated. Switch to Microsoft.Data.SqlClient (note Encrypt=true is the default from version 4).");
    public static readonly Rule EntityFramework6 = new("LDM131", "Entity Framework 6", Severity.Medium, "data", 5,
        "EF6 (6.3+) runs on .NET, so it need not block the move. Plan EF Core separately: lazy loading, EDMX models and some LINQ translations differ.");

    // Security
    public static readonly Rule WeakCrypto = new("LDM140", "Weak or obsolete cryptography", Severity.High, "security", 3,
        "MD5, SHA-1, DES, TripleDES and RC2 are unsuitable for security purposes; RNGCryptoServiceProvider is obsolete (SYSLIB0023). Use SHA-256+, AES-GCM, PBKDF2/Argon2 for passwords and RandomNumberGenerator.");
    public static readonly Rule PlaintextSecret = new("LDM141", "Secret in a configuration file", Severity.High, "security", 2,
        "Move credentials to a secret store (Azure Key Vault, AWS Secrets Manager, user-secrets for development), prefer managed identities, and rotate the exposed secret.");
    public static readonly Rule MachineKey = new("LDM142", "machineKey in configuration", Severity.Medium, "security", 3,
        "ASP.NET Core uses Data Protection keys. Share keys between old and new apps during a strangler migration (System.Web adapters) and keep keys out of source control.");

    // Configuration features
    public static readonly Rule FormsAuthentication = new("LDM150", "Forms or Windows authentication in web.config", Severity.High, "web", 5,
        "Re-implement with ASP.NET Core cookie, OpenID Connect or Negotiate authentication; share the auth ticket during incremental migration.");
    public static readonly Rule ModulesHandlers = new("LDM151", "HTTP modules or handlers", Severity.Medium, "web", 3,
        "Port IHttpModule/IHttpHandler to middleware or endpoints.");
    public static readonly Rule SessionState = new("LDM152", "Session state", Severity.Medium, "web", 3,
        "ASP.NET Core session needs IDistributedCache (Redis, SQL Server) and is not locked per request; review concurrent access.");
    public static readonly Rule WcfConfig = new("LDM153", "system.serviceModel configuration", Severity.High, "services", 3,
        "Bindings and behaviours move to code (CoreWCF) or are replaced with gRPC/REST configuration.");

    // Packages
    public static readonly Rule LegacyPackage = new("LDM160", "Package tied to .NET Framework or deprecated", Severity.High, "packages", 3,
        "Replace with the package named in the finding.");

    public static IReadOnlyList<Rule> All { get; } = typeof(Rules)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.FieldType == typeof(Rule))
        .Select(f => (Rule)f.GetValue(null)!)
        .OrderBy(r => r.Id, StringComparer.Ordinal)
        .ToList();

    /// <summary>Patterns applied to sanitized C# (comments and literals blanked).</summary>
    internal static readonly (Rule Rule, Regex Pattern)[] CodePatterns =
    [
        (SystemWeb, R(@"\busing\s+System\.Web(\.[A-Za-z.]+)?\s*;|\bHttpContext\.Current\b|\bSystem\.Web\.(Mvc|Http|UI|HttpContext)\b")),
        (GlobalAsax, R(@":\s*(System\.Web\.)?HttpApplication\b")),
        (OwinKatana, R(@"\busing\s+(Microsoft\.)?Owin\b|\bIAppBuilder\b|\[assembly:\s*OwinStartup\b")),
        (WcfService, R(@"\[\s*ServiceContract\b|\bnew\s+ServiceHost\b|\bServiceHostFactory\b")),
        (WcfClient, R(@"\bChannelFactory\s*<|\bClientBase\s*<")),
        (Remoting, R(@"\bSystem\.Runtime\.Remoting\b|\bRemotingServices\b|\bRemotingConfiguration\b")),
        (EnterpriseServices, R(@"\bSystem\.EnterpriseServices\b|\bServicedComponent\b")),
        (LegacyHttpClient, R(@"\bnew\s+WebClient\s*\(|\bWebRequest\.Create(Http)?\s*\(|\bHttpWebRequest\b")),
        (UnsafeSerializer, R(@"\b(BinaryFormatter|SoapFormatter|NetDataContractSerializer|LosFormatter|ObjectStateFormatter)\b")),
        (AppDomains, R(@"\bAppDomain\.CreateDomain\s*\(")),
        (ThreadAbort, R(@"\b\w*[Tt]hread\w*\s*\.\s*Abort\s*\(|\bThread\.ResetAbort\s*\(")),
        (CodeAccessSecurity, R(@"\bSecurityAction\.(Demand|Assert|Deny|PermitOnly|LinkDemand|InheritanceDemand|RequestMinimum|RequestOptional|RequestRefuse)\b|\bAllowPartiallyTrustedCallers\b|\bnew\s+PermissionSet\s*\(")),
        (SystemDrawing, R(@"\busing\s+System\.Drawing(\.Imaging|\.Drawing2D)?\s*;|\bSystem\.Drawing\.(Bitmap|Image|Graphics)\b")),
        (Registry, R(@"\bRegistry\.(LocalMachine|CurrentUser|ClassesRoot|Users|CurrentConfig)\b|\bRegistryKey\b")),
        (ConfigurationManager, R(@"\bConfigurationManager\.(AppSettings|ConnectionStrings|GetSection)\b")),
        (LegacySqlClient, R(@"\busing\s+System\.Data\.SqlClient\s*;|\bSystem\.Data\.SqlClient\.")),
        (EntityFramework6, R(@"\busing\s+System\.Data\.Entity(\.[A-Za-z.]+)?\s*;")),
        (WeakCrypto, R(@"\b(MD5|SHA1|DES|TripleDES|RC2)\.Create\s*\(|\bnew\s+(MD5|SHA1|DES|TripleDES|RC2)CryptoServiceProvider\b|\b(MD5|SHA1)Managed\b|\bRNGCryptoServiceProvider\b")),
    ];

    /// <summary>NuGet packages that pin a project to .NET Framework or are deprecated, with the replacement.</summary>
    internal static readonly (Regex Id, string Replacement)[] Packages =
    [
        (R(@"^Microsoft\.AspNet\.(Mvc|WebApi.*|WebPages|Razor)$"), "ASP.NET Core (built in)"),
        (R(@"^Microsoft\.AspNet\.SignalR(\..*)?$"), "ASP.NET Core SignalR (built in)"),
        (R(@"^Microsoft\.AspNet\.Web\.Optimization$"), "a front-end build (Vite, esbuild) or WebOptimizer"),
        (R(@"^Microsoft\.Web\.Infrastructure$"), "nothing (ASP.NET Core)"),
        (R(@"^(Microsoft\.Owin(\..*)?|Owin)$"), "ASP.NET Core middleware"),
        (R(@"^WindowsAzure\.Storage$"), "Azure.Storage.Blobs / Azure.Storage.Queues / Azure.Data.Tables"),
        (R(@"^(Microsoft\.Azure\.ServiceBus|WindowsAzure\.ServiceBus)$"), "Azure.Messaging.ServiceBus"),
        (R(@"^Microsoft\.Azure\.DocumentDB(\.Core)?$"), "Microsoft.Azure.Cosmos"),
        (R(@"^System\.Data\.SqlClient$"), "Microsoft.Data.SqlClient"),
        (R(@"^Microsoft\.ApplicationInsights\.Web$"), "Microsoft.ApplicationInsights.AspNetCore or OpenTelemetry"),
    ];

    private static Regex R(string pattern) => new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
}
