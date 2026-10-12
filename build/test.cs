#!/usr/bin/env -S dotnet --
#:property NoWarn=SA1400,SA1503,SA1519,SA1116,SA1117,SA1122,SA1649,IDE0008,IDE0011,IDE0040,SA1500

#nullable enable

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Xml.Linq;

try
{
    if (args.Length == 0 || args[0] is "help" or "-h" or "--help")
    {
        Help();
        return 0;
    }

    var command = args[0];
    var options = Options.Parse(args[1..]);
    return command switch
    {
        "e2e" => await RunE2e(options),
        "fresh" => await RunFresh(options),
        "report" => await ShowReport(options),
        _ => throw new ArgumentException($"Unknown command '{command}'. Run with 'help'.")
    };
}
catch (Exception error)
{
    Console.Error.WriteLine($"ERROR: {error.Message}");
    return 1;
}

static void Help()
{
    var root = FindRepositoryRoot();
    Console.WriteLine(File.ReadAllText(Path.Combine(root, "build", "test-help.md")));
}

static async Task<int> RunE2e(Options options)
{
    options.Validate("e2e", "lane", "candidate-project", "candidate-port");
    var candidateProject = options.Get("candidate-project");
    var candidatePort = options.Get("candidate-port");
    if ((candidateProject is null) != (candidatePort is null))
        throw new ArgumentException("--candidate-project and --candidate-port must be used together.");
    var lanes = options.Get("lane", "all") switch
    {
        "v17" => new[] { "v17" },
        "v18" => new[] { "v18" },
        "all" when candidateProject is null => new[] { "v17", "v18" },
        "all" => throw new ArgumentException("Candidate E2E requires one lane."),
        var lane => throw new ArgumentException($"--lane must be v17, v18 or all (got '{lane}').")
    };

    var root = FindRepositoryRoot();
    var client = Path.Combine(root, "src", "Articulate.Web", "Client");

    if (candidateProject is not null && candidatePort is not null)
    {
        var lane = lanes[0];
        if (!Regex.IsMatch(candidateProject, $@"^(?:art_e2e_candidate|art_e2e_native)_{lane}_[a-f0-9]{{32}}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("--candidate-project must be a generated candidate project name for the selected lane.");
        if (!int.TryParse(candidatePort, out var parsedPort) || parsedPort is < 1024 or > 65535 || IsProtectedPort(parsedPort))
            throw new ArgumentException("--candidate-port must be a non-protected localhost port.");
        await VerifyCandidate(root, candidateProject, parsedPort);
    }

    foreach (var lane in lanes)
    {
        var port = candidateProject is null ? lane == "v17" ? "19443" : "19444" : candidatePort!;
        var exit = await RunPlaywright(root, lane, port, candidateProject);
        if (exit != 0) return exit;
    }
    return 0;
}

static async Task<int> RunPlaywright(string root, string lane, string port, string? candidate = null, IReadOnlyDictionary<string, string>? environment = null)
{
    var client = Path.Combine(root, "src", "Articulate.Web", "Client");
    var cli = Path.Combine(client, "node_modules", "@playwright", "test", "cli.js");
    RequireFile(cli, "Playwright is not installed. Install the Client dependencies first (see src/Articulate.Web/Client/e2e/README.md).");
    var psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("NODE_BIN") ?? "node") { WorkingDirectory = client, UseShellExecute = false };
    if (environment is not null) foreach (var (key, value) in environment) psi.Environment[key] = value;
    foreach (var arg in new[] { cli, "test", "--config", "playwright.config.ts" }) psi.ArgumentList.Add(arg);
    psi.Environment["ARTICULATE_E2E_BASE_URL"] = $"https://localhost:{port}";
    psi.Environment.Remove("ARTICULATE_E2E_CANDIDATE_PROJECT");
    psi.Environment.Remove("ARTICULATE_E2E_CANDIDATE_PORT");
    if (candidate is not null)
    {
        psi.Environment["ARTICULATE_E2E_CANDIDATE_PROJECT"] = candidate;
        psi.Environment["ARTICULATE_E2E_CANDIDATE_PORT"] = port;
    }
    psi.Environment["PLAYWRIGHT_HTML_OUTPUT_DIR"] = Path.Combine(client, $"e2e-report-{candidate ?? lane}");
    psi.Environment["PLAYWRIGHT_OUTPUT_DIR"] = Path.Combine(client, $"e2e-results-{candidate ?? lane}");
    Console.WriteLine($"E2E {lane}: https://localhost:{port}");
    using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start Node.");
    await process.WaitForExitAsync();
    return process.ExitCode;
}

static async Task<int> RunFresh(Options options)
{
    options.Validate("fresh", "lane");
    var lanes = options.Get("lane", "all") switch
    {
        "v17" => new[] { "v17" }, "v18" => new[] { "v18" }, "all" => new[] { "v17", "v18" },
        var lane => throw new ArgumentException($"--lane must be v17, v18 or all (got '{lane}').")
    };
    var root = FindRepositoryRoot();
    var dotnet = Environment.GetEnvironmentVariable("DOTNET_BIN") ?? "dotnet";
    var node = Environment.GetEnvironmentVariable("NODE_BIN") ?? "node";
    foreach (var lane in lanes)
    {
        var packages = Path.Combine(root, "build", "Release", lane);
        var main = Directory.EnumerateFiles(packages, "Articulate.*.nupkg")
            .Where(file => Regex.IsMatch(Path.GetFileName(file), @"^Articulate\.[0-9]", RegexOptions.CultureInvariant)).ToArray();
        if (main.Length != 1) throw new InvalidOperationException($"Expected exactly one main package in {packages}.");
        var version = Path.GetFileName(main[0])["Articulate.".Length..^".nupkg".Length];
        var cms = (await Capture(dotnet, new[] { "msbuild", Path.Combine(root, "src", "Articulate.Web", "Articulate.Web.csproj"), "-getProperty:UmbracoCmsPackageVersion", $"-p:ArticulatePackageLane={lane}", "-nodeReuse:false" }, root)).Trim();
        if (string.IsNullOrWhiteSpace(cms)) throw new InvalidOperationException("The lane CMS version is missing.");
        var targetFramework = (await Capture(dotnet, new[] { "msbuild", Path.Combine(root, "src", "Articulate.Web", "Articulate.Web.csproj"), "-getProperty:TargetFramework", $"-p:ArticulatePackageLane={lane}", "-nodeReuse:false" }, root)).Trim();
        if (string.IsNullOrWhiteSpace(targetFramework)) throw new InvalidOperationException("The lane target framework is missing.");
        var candidate = $"art_e2e_native_{lane}_{Guid.NewGuid():N}";
        var directory = Path.Combine(Path.GetTempPath(), candidate);
        if (Directory.Exists(directory)) throw new IOException("Native candidate directory already exists.");
        Directory.CreateDirectory(directory);
        var evidence = Path.Combine(root, ".temp", candidate);
        (Process Process, Task Output, Task Error)? host = null;
        var generation = 0;
        Exception? failure = null;
        try
        {
            await RunCommand(node, new[] { Path.Combine(root, "build", "smoke-package.mjs"), packages }, root);
            var source = Path.Combine(root, "docker", "src");
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                if (relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")) continue;
                if (Path.GetExtension(file) is not (".cs" or ".csproj") && relative != "appsettings.json") continue;
                var target = Path.Combine(directory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            new XDocument(new XElement("configuration", new XElement("packageSources", new XElement("clear"),
                new XElement("add", new XAttribute("key", "lane"), new XAttribute("value", packages)),
                new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json")))))
                .Save(Path.Combine(directory, "nuget.config"));
            var password = $"E2e!{Guid.NewGuid():N}";
            var pfxPassword = Guid.NewGuid().ToString("N");
            var clientSecret = Guid.NewGuid().ToString("N");
            var certificate = Path.Combine(directory, "localhost.pfx");
            using (var key = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var names = new SubjectAlternativeNameBuilder();
                names.AddDnsName("localhost");
                request.CertificateExtensions.Add(names.Build());
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
                using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
                await File.WriteAllBytesAsync(certificate, cert.Export(X509ContentType.Pfx, pfxPassword));
            }
            int port;
            do port = FindFreePort(); while (IsProtectedPort(port));
            var url = $"https://localhost:{port}";
            var environment = new Dictionary<string, string>
            {
                ["NUGET_PACKAGES"] = Path.Combine(directory, "packages"),
                ["ASPNETCORE_ENVIRONMENT"] = "Production", ["ASPNETCORE_URLS"] = url,
                ["Kestrel__Certificates__Default__Path"] = certificate, ["Kestrel__Certificates__Default__Password"] = pfxPassword,
                ["ConnectionStrings__umbracoDbDSN"] = $"Data Source={Path.Combine(directory, "database.sqlite")};Cache=Shared;Foreign Keys=True;Pooling=True",
                ["ConnectionStrings__umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
                ["Umbraco__CMS__Runtime__Mode"] = "BackofficeDevelopment",
                ["Umbraco__CMS__Unattended__UnattendedUserName"] = "E2E candidate",
                ["Umbraco__CMS__Unattended__UnattendedUserEmail"] = "e2e@localhost",
                ["Umbraco__CMS__Unattended__UnattendedUserPassword"] = password,
                ["Umbraco__CMS__Security__BackOfficeHost"] = url, ["Umbraco__CMS__Global__UseHttps"] = "true",
                ["Umbraco__CMS__WebRouting__UmbracoApplicationUrl"] = url + "/",
                ["Articulate__TestSite__Enabled"] = "true", ["Articulate__TestSite__ClientSecret"] = clientSecret,
                ["Articulate__ManagementApi__OpenIddict__Client__RedirectUris__0"] = url + "/a-new/",
                ["Articulate__ManagementApi__OpenIddict__Client__PostLogoutRedirectUris__0"] = url + "/",
                ["ARTICULATE_TEST_SITE_CLIENT_SECRET"] = clientSecret, ["UMBRACO_PUBLIC_URL"] = url,
                ["ARTICULATE_E2E_CANDIDATE_PROJECT"] = candidate, ["ARTICULATE_E2E_CANDIDATE_PORT"] = port.ToString(),
                ["TIMEOUT_SECONDS"] = "180",
                ["MSBUILDDISABLENODEREUSE"] = "1", ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0" // why: see Capture
            };
            var project = Path.Combine(directory, "ArticulateDockerSite.csproj");
            // The copied host is outside the repository's central-package and implicit-using settings.
            var projectXml = XDocument.Load(project);
            var properties = projectXml.Root!.Element("PropertyGroup")!;
            properties.Add(new XElement("ImplicitUsings", "enable"),
                new XElement("ArticulatePackageVersion", version), new XElement("UmbracoCmsPackageVersion", cms));
            properties.SetElementValue("TargetFramework", targetFramework);
            properties.SetElementValue("CompressionEnabled", "false");
            foreach (var reference in projectXml.Descendants("PackageReference"))
            {
                if (reference.Attribute("VersionOverride") is not { } overridden) continue;
                reference.SetAttributeValue("Version", overridden.Value);
                overridden.Remove();
            }
            projectXml.Save(project);
            var published = Path.Combine(directory, "published");
            await RunCommand(dotnet, new[] { "publish", project, "--configuration", "Release", "--output", published }, root, environment);
            foreach (var package in Directory.EnumerateFiles(packages, "*.nupkg"))
            {
                var name = Path.GetFileNameWithoutExtension(package);
                var id = name.StartsWith("Articulate.Theme.Sample.", StringComparison.Ordinal) ? "articulate.theme.sample" : "articulate";
                var restored = Path.Combine(directory, "packages", id, version.ToLowerInvariant(), name.ToLowerInvariant() + ".nupkg.sha512");
                var expected = Convert.ToBase64String(SHA512.HashData(await File.ReadAllBytesAsync(package)));
                if (!File.Exists(restored) || (await File.ReadAllTextAsync(restored)).Trim() != expected)
                    throw new InvalidOperationException($"The restored package differs from the inspected archive: {name}.");
            }
            foreach (var mode in new[] { "BackofficeDevelopment", "Production" })
            {
                environment["Umbraco__CMS__Runtime__Mode"] = mode;
                var start = Command(dotnet, new[] { Path.Combine(published, "ArticulateDockerSite.dll") }, published, environment);
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                generation++;
                Directory.CreateDirectory(evidence);
                var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the owned native host.");
                // Stream to disk as output arrives so a step timeout still leaves host evidence.
                host = (process, Drain(process.StandardOutput.BaseStream, Path.Combine(evidence, $"native-host-{generation}.log")),
                    Drain(process.StandardError.BaseStream, Path.Combine(evidence, $"native-host-{generation}.stderr.log")));
                foreach (var smoke in mode == "Production" ? new[] { "smoke", "index-ready" } : new[] { "publish", "confirm" })
                    await RunCommand(node, new[] { Path.Combine(root, "docker", "smoke.mjs"), smoke }, root, environment);
                if (mode == "Production") break;
                var preparedHost = host.Value;
                host = null;
                await StopHost(preparedHost);
            }
            var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(main[0])));
            Console.WriteLine($"Native candidate receipt: lane={lane} package={Path.GetFileName(main[0])} sha256={hash} cms={cms} candidate={candidate} pid={host!.Value.Process.Id} url={url} package-restore=exact-archives indexing=owned-external-key");
            var exit = await RunPlaywright(root, lane, port.ToString(), candidate, environment);
            if (exit != 0)
            {
                // Report cleanup errors without letting them replace the Playwright exit code.
                failure = new InvalidOperationException($"Playwright exited {exit}.");
                return exit;
            }
        }
        catch (Exception error)
        {
            failure = error;
            throw;
        }
        finally
        {
            var cleanup = new List<Exception>();
            try
            {
                if (host is not null)
                {
                    var ownedHost = host.Value;
                    host = null;
                    await StopHost(ownedHost);
                }
            }
            catch (Exception error) { cleanup.Add(error); }
            try
            {
                Directory.Delete(directory, recursive: true);
                Console.WriteLine($"Native candidate cleanup verified: {candidate}");
            }
            catch (Exception error) { cleanup.Add(error); }
            // A cleanup error must not mask the original failure.
            if (failure is not null) foreach (var error in cleanup) Console.Error.WriteLine($"Cleanup error: {error.Message}");
            else if (cleanup.Count > 0) throw new AggregateException(cleanup);
        }
    }
    return 0;
}

static ProcessStartInfo Command(string file, IEnumerable<string> args, string cwd, IReadOnlyDictionary<string, string>? environment = null)
{
    var start = new ProcessStartInfo(file) { WorkingDirectory = cwd, UseShellExecute = false };
    foreach (var arg in args) start.ArgumentList.Add(arg);
    if (environment is not null) foreach (var (key, value) in environment) start.Environment[key] = value;
    return start;
}

static async Task RunCommand(string file, IEnumerable<string> args, string cwd, IReadOnlyDictionary<string, string>? environment = null)
{
    using var process = Process.Start(Command(file, args, cwd, environment)) ?? throw new InvalidOperationException($"Could not start {file}.");
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new InvalidOperationException($"{Path.GetFileName(file)} exited {process.ExitCode}.");
}

static async Task StopHost((Process Process, Task Output, Task Error) host)
{
    using var process = host.Process;
    if (!process.HasExited) process.Kill(entireProcessTree: true);
    await process.WaitForExitAsync();
    await Task.WhenAll(host.Output, host.Error);
}

static async Task Drain(Stream source, string path)
{
    // Unbuffered so a killed job keeps everything the host wrote.
    await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 0);
    await source.CopyToAsync(file);
}

static async Task VerifyCandidate(string root, string project, int port)
{
    var composeFile = Path.Combine(root, "docker", "docker-compose.yml");
    var containerId = await Capture("docker", new[] { "compose", "--project-name", project, "-f", composeFile, "ps", "-q", "caddy" }, root);
    containerId = containerId.Trim();
    if (containerId.Length == 0) throw new InvalidOperationException("Candidate Compose project has no Caddy container.");

    var identity = await Capture("docker", new[]
    {
        "inspect", "--format", "{{ index .Config.Labels \"com.docker.compose.project\" }}|{{ index .Config.Labels \"com.docker.compose.service\" }}|{{ .State.Status }}", containerId
    }, root);
    if (identity.Trim() != $"{project}|caddy|running")
        throw new InvalidOperationException("Candidate Caddy container identity does not match its Compose project.");

    var publishedPort = await Capture("docker", new[] { "compose", "--project-name", project, "-f", composeFile, "port", "caddy", "18443" }, root);
    if (!publishedPort.Trim().EndsWith($":{port}", StringComparison.Ordinal))
        throw new InvalidOperationException("Candidate Compose project does not own the requested HTTPS port.");
}

static async Task<string> Capture(string file, IEnumerable<string> args, string cwd)
{
    var psi = new ProcessStartInfo(file) { UseShellExecute = false, WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in args) psi.ArgumentList.Add(arg);
    // why: MSBuild node reuse / build server children inherit the redirected pipes and keep ReadToEnd blocked.
    psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    psi.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
    using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}.");
    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    await Task.WhenAll(outputTask, errorTask);
    await process.WaitForExitAsync();
    var output = outputTask.Result;
    var error = errorTask.Result;
    if (process.ExitCode != 0) throw new InvalidOperationException($"{file} exited {process.ExitCode}: {error.Trim()}");
    return output;
}

static async Task<int> ShowReport(Options options)
{
    options.Validate("report", "lane");
    var lane = options.Get("lane") ?? throw new ArgumentException("report requires --lane v17 or v18.");
    if (lane is not ("v17" or "v18")) throw new ArgumentException("--lane must be v17 or v18.");
    var root = FindRepositoryRoot();
    var client = Path.Combine(root, "src", "Articulate.Web", "Client");
    var report = Path.Combine(client, $"e2e-report-{lane}");
    if (!File.Exists(Path.Combine(report, "index.html")))
        report = Directory.EnumerateDirectories(client, $"e2e-report-art_e2e_*_{lane}_*").Where(d => File.Exists(Path.Combine(d, "index.html")))
            .OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault() ?? report;
    RequireFile(Path.Combine(report, "index.html"), $"No {lane} HTML report exists (manual or fresh/candidate) under '{client}'. Run 'e2e --lane {lane}' or 'fresh --lane {lane}' first.");
    var cli = Path.Combine(client, "node_modules", "@playwright", "test", "cli.js");
    RequireFile(cli, "Playwright is not installed. Install the Client dependencies first (see src/Articulate.Web/Client/e2e/README.md).");

    var psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("NODE_BIN") ?? "node") { WorkingDirectory = client, UseShellExecute = false };
    psi.ArgumentList.Add(cli);
    psi.ArgumentList.Add("show-report");
    psi.ArgumentList.Add(report);
    psi.ArgumentList.Add("--host");
    psi.ArgumentList.Add("127.0.0.1");
    psi.ArgumentList.Add("--port");
    psi.ArgumentList.Add("0");
    psi.Environment["PLAYWRIGHT_HTML_OUTPUT_DIR"] = report;
    Console.WriteLine($"Starting {lane} report server (Ctrl+C to stop). Playwright prints the actual local URL.");
    using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start Node.");
    await process.WaitForExitAsync();
    return process.ExitCode;
}

static bool IsProtectedPort(int port) => port is 18443 or 18444 or 19443 or 19444;

static int FindFreePort()
{
    // shortcut: port can be taken between probe and bind (TOCTOU); acceptable for single-runner CI
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return ((IPEndPoint)listener.LocalEndpoint).Port;
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "build", "test.cs"))) return directory.FullName;
    throw new DirectoryNotFoundException("Run this command from the repository or one of its subdirectories.");
}

static void RequireFile(string path, string message)
{
    if (!File.Exists(path)) throw new FileNotFoundException(message, path);
}

sealed class Options
{
    private readonly Dictionary<string, string?> values;
    private Options(Dictionary<string, string?> values) => this.values = values;

    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string?>();
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unexpected argument '{key}'.");
            key = key[2..];
            if (!values.TryAdd(key, null)) throw new ArgumentException($"Duplicate option '--{key}'.");
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option '--{key}' requires a value.");
            values[key] = args[++i];
        }
        return new Options(values);
    }

    public void Validate(string command, params string[] allowed)
    {
        var unknown = values.Keys.Except(allowed, StringComparer.Ordinal).FirstOrDefault();
        if (unknown is not null) throw new ArgumentException($"Unknown option '--{unknown}' for {command}.");
    }

    public string? Get(string key, string? fallback = null) => values.TryGetValue(key, out var value) ? value : fallback;
}
