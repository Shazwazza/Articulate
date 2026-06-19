#!/usr/bin/env dotnet
#:property RunAnalyzers=false
#nullable enable

using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

return await BuildApp.RunAsync(args);

static class BuildApp
{
    static readonly string Repo = FindRepo();
    static readonly string BuildDir = Path.Combine(Repo, "build");
    static readonly string Solution = Path.Combine(Repo, "src", "Articulate.sln");
    static readonly string DockerDir = Path.Combine(BuildDir, "docker-site");

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                Help();
                return 0;
            }

            var command = args[0];
            var options = Options.Parse(args[1..]);
            switch (command)
            {
                case "build":
                    await BuildAsync(options);
                    break;
                case "client":
                    await ClientAsync(options);
                    break;
                case "site":
                    await SiteAsync(options);
                    break;
                case "docker-build":
                    await DockerBuildAsync(options);
                    break;
                case "docker-dev":
                    await DockerDevAsync(options);
                    break;
                case "docker-prod":
                    await DockerProdAsync(options);
                    break;
                case "docker-test":
                    await DockerTestAsync(options);
                    break;
                default:
                throw new ArgumentException($"Unknown command '{command}'. Run 'dotnet run --file build/build.cs -- --help'.");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }

    static void Help() => Console.WriteLine(
        """
        Articulate build utility

          dotnet run --file build/build.cs -- build [--lane v17|v18] [--configuration Release|Debug]
                                                      [--tests] [--client true|false] [--sample]
          dotnet run --file build/build.cs -- client --lane v17|v18
          dotnet run --file build/build.cs -- site --lane v17|v18 [--configuration Debug] [--reset]
          dotnet run --file build/build.cs -- docker-build [--lane v17|v18] [--tag image:tag]
          dotnet run --file build/build.cs -- docker-dev [--skip-smoke] [--reset]
          dotnet run --file build/build.cs -- docker-prod
          dotnet run --file build/build.cs -- docker-test [--lane v17|v18|all] [--keep] [--skip-smoke]

        Environment variables remain supported for CI and local overrides.
        """);

    static async Task BuildAsync(Options options)
    {
        var started = Stopwatch.StartNew();
        var lane = Lane(options);
        var configuration = options.Value("configuration") ?? Env("BUILD_CONFIGURATION") ?? "Release";
        var inCi = IsTrue(Env("CI")) || IsTrue(Env("GITHUB_ACTIONS"));
        var runTests = options.Flag("tests") || BoolOption(options, "tests", "RUN_TESTS", inCi);
        var clientBuild = BoolOption(options, "client", "ENABLE_CLIENT_BUILD", inCi || configuration == "Release");
        var sample = options.Flag("sample") || BoolOption(options, "sample", "PACK_SAMPLE_THEME", !inCi);
        var clientVersion = lane == "v18" ? "18" : "17";
        var major = lane == "v18" ? "7" : "6";
        var releaseDir = Path.Combine(BuildDir, configuration, lane);
        Directory.CreateDirectory(releaseDir);

        var packageVersion = Env("ARTICULATE_PACKAGE_VERSION");
        if (lane == "v18" && string.IsNullOrWhiteSpace(packageVersion))
        {
            var baseVersion = (await File.ReadAllTextAsync(Path.Combine(BuildDir, "v18-version.txt"))).Trim();
            if (!Regex.IsMatch(baseVersion, @"^7\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$"))
                throw new InvalidOperationException($"Invalid v18 base version '{baseVersion}'.");
            var v17 = (await CaptureAsync("nbgv", ["get-version", "-v", "SemVer2"], Repo)).Trim();
            var match = Regex.Match(v17, @"^6\.1\.\d+(?:-(.+))?$");
            if (!match.Success) throw new InvalidOperationException($"Expected NBGV 6.1.x, got '{v17}'.");
            packageVersion = baseVersion + (match.Groups[1].Success ? $".{match.Groups[1].Value}" : "");
        }

        foreach (var file in Directory.EnumerateFiles(releaseDir))
            if (Regex.IsMatch(Path.GetFileName(file), $@"^Articulate(?:\.Theme\.Sample)?\.{major}\..*\.s?nupkg$"))
                File.Delete(file);

        var props = new List<string>
        {
            $"-p:EnableClientBuild={clientBuild.ToString().ToLowerInvariant()}",
            $"-p:ArticulatePackageLane={lane}",
            $"-p:UmbracoClientVersion={clientVersion}",
        };
        if (!string.IsNullOrWhiteSpace(packageVersion))
            props.Add($"-p:ArticulatePackageVersion={packageVersion}");

        var clean = options.Flag("clean");
        Console.WriteLine($"Build: {lane}, {configuration}, package {packageVersion ?? "NBGV"}{(clean ? " (Clean Build)" : "")}");
        // MSBuild invokes pnpm without an interactive terminal; prevent package-manager prompts.
        Environment.SetEnvironmentVariable("CI", "true");
        
        if (clean)
        {
            await RunAsync("dotnet", ["build-server", "shutdown"], Repo, allowFailure: true);
            DeleteBuildOutputs(Path.Combine(Repo, "src"));
            DeleteDirectory(Path.Combine(BuildDir, "ClientAssets"));
            DeleteDirectory(Path.Combine(Repo, "src", "Articulate.Web", "wwwroot", "App_Plugins", "Articulate", "BackOffice"));
        }

        if (clientBuild)
        {
            var clientRoot = Path.Combine(Repo, "src", "Articulate.Web", "Client");
            if (clean)
            {
                await RunAsync("pnpm", ["--workspace-concurrency=1", "-r", "run", "clean"], clientRoot);
            }
            
            var pnpmArgs = inCi
                ? new[] { "install", "--frozen-lockfile", "--prefer-offline" }
                : new[] { "install", "--prefer-offline" };
            await RunAsync("pnpm", pnpmArgs, clientRoot);
        }

        var restoreArgs = new List<string> { "restore", Solution, "-v", "minimal", "-p:RestoreUseStaticGraphEvaluation=true" };
        if (inCi)
        {
            restoreArgs.Add("--locked-mode");
        }
        restoreArgs.AddRange(props);
        await RunAsync("dotnet", [.. restoreArgs], Repo);
        await RunAsync("dotnet", ["build", Solution, "-c", configuration, "--no-restore", "-v", "minimal",
            "-m:1", "-p:BuildInParallel=false", "-p:UseSharedCompilation=false", .. props], Repo);
        if (runTests)
            await RunAsync("dotnet", ["test", Solution, "-c", configuration, "--no-restore", "--no-build",
                "-v", "minimal", "-m:1", "-p:BuildInParallel=false", .. props], Repo);

        var projects = new List<string> { Path.Combine(Repo, "src", "Articulate.Web", "Articulate.Web.csproj") };
        if (sample) projects.Add(Path.Combine(Repo, "src", "Articulate.Theme.Sample", "Articulate.Theme.Sample.csproj"));
        foreach (var project in projects)
            await RunAsync("dotnet", ["pack", project, "-c", configuration, "--no-restore", "--no-build",
                "-o", releaseDir, "-v", "minimal", "-m:1", "-p:BuildInParallel=false", .. props], Repo);

        Console.WriteLine($"Completed in {started.Elapsed.TotalSeconds:N1}s: {releaseDir}");
    }

    static async Task ClientAsync(Options options)
    {
        var lane = Lane(options);
        var root = Path.Combine(Repo, "src", "Articulate.Web", "Client");
        await RunAsync("pnpm", ["install"], root);
        var dir = Path.Combine(root, lane);
        await RunAsync("pnpm", ["run", "check"], dir);
        await RunAsync("pnpm", ["run", "build"], dir);
        await RunAsync("pnpm", ["run", "lint"], dir);
    }

    static async Task SiteAsync(Options options)
    {
        var lane = Lane(options);
        var configuration = options.Value("configuration") ?? "Debug";
        var project = Path.Combine(Repo, "src", "Articulate.Tests.Website", "Articulate.Tests.Website.csproj");
        if (options.Flag("reset"))
            DeleteDirectory(Path.Combine(Repo, "src", "Articulate.Tests.Website", "umbraco"));
        await RunAsync("dotnet", ["run", "-c", configuration, "--project", project,
            $"-p:ArticulatePackageLane={lane}"], Repo);
    }

    static async Task DockerBuildAsync(Options options)
    {
        var lane = Lane(options);
        await EnsurePackagesAsync(lane);
        var tag = options.Value("tag") ?? $"articulate-local:{lane}";
        var version = lane == "v18" ? "[18.0.0-*,19.0.0)" : "[17.4.0,18.0.0)";
        await RunAsync("docker", ["build", "--file", "Dockerfile", "--target", "chiseled", "--tag", tag,
            "--build-arg", $"PACKAGE_SOURCE=build/Release/{lane}", "--build-arg", $"UMBRACO_CMS_VERSION={version}", "."], Repo);
    }

    static async Task DockerDevAsync(Options options)
    {
        Require("docker");
        var url = Env("UMBRACO_PUBLIC_URL") ?? "https://localhost:18443/";
        SetDefault("UMBRACO_RUNTIME_MODE", "BackofficeDevelopment");
        if (options.Flag("reset")) await ComposeAsync(["down", "-v"]);
        await ComposeAsync(["up", "-d"]);
        await WaitForAsync(new Uri(new Uri(url), "umbraco/"));
        if (!options.Flag("skip-smoke"))
        {
            RequireSecret();
            await SmokeAsync("publish");
            await SmokeAsync("confirm");
        }
    }

    static async Task DockerProdAsync(Options options)
    {
        RequireSecret();
        SetDefault("UMBRACO_RUNTIME_MODE", "Production");
        SetDefault("UMBRACO_PUBLIC_HOST", "https://localhost:18443");
        SetDefault("UMBRACO_PUBLIC_URL", "https://localhost:18443/");
        await ComposeAsync(["up", "-d", "--force-recreate"]);
        await WaitForAsync(new Uri(new Uri(Env("UMBRACO_PUBLIC_URL")!), "umbraco/"));
        await SmokeAsync("smoke");
        await SmokeAsync("theme");
    }

    static async Task DockerTestAsync(Options options)
    {
        var requested = options.Value("lane") ?? "all";
        if (requested is not ("v17" or "v18" or "all"))
            throw new ArgumentException("--lane must be v17, v18, or all.");
        var lanes = requested == "all" ? new[] { "v17", "v18" } : new[] { requested };
        var keep = options.Flag("keep");
        var skipSmoke = options.Flag("skip-smoke");

        foreach (var lane in lanes)
        {
            await EnsurePackagesAsync(lane);
            ConfigureLane(lane);
            try
            {
                await ComposeAsync(["build", "--no-cache", "--pull"]);
                await DockerDevAsync(Options.FromFlags(skipSmoke ? ["skip-smoke"] : []));
                if (!skipSmoke) await DockerProdAsync(new Options());
                Console.WriteLine($"PASSED: {lane}");
            }
            finally
            {
                if (!keep) await ComposeAsync(["down", "-v"], allowFailure: true);
            }
        }
    }

    static async Task EnsurePackagesAsync(string lane)
    {
        var major = lane == "v18" ? "7" : "6";
        var dir = Path.Combine(BuildDir, "Release", lane);
        var package = Directory.Exists(dir) && Directory.EnumerateFiles(dir, $"Articulate.{major}.*.nupkg").Any();
        var sample = Directory.Exists(dir) && Directory.EnumerateFiles(dir, $"Articulate.Theme.Sample.{major}.*.nupkg").Any();
        if (!package || !sample)
            await BuildAsync(Options.FromValues(("lane", lane), ("sample", "true")));
    }

    static void ConfigureLane(string lane)
    {
        var is18 = lane == "v18";
        var https = is18 ? "18018" : "17017";
        var http = is18 ? "18080" : "17080";
        Environment.SetEnvironmentVariable("COMPOSE_PROJECT_NAME", $"art_{lane}");
        Environment.SetEnvironmentVariable("COMPOSE_VOLUME_PREFIX", $"art_{lane}");
        Environment.SetEnvironmentVariable("IMAGE_TAG", $"articulate-local:{lane}");
        Environment.SetEnvironmentVariable("PACKAGE_SOURCE", $"build/Release/{lane}");
        Environment.SetEnvironmentVariable("UMBRACO_CMS_VERSION", is18 ? "[18.0.0-*,19.0.0)" : "[17.4.0,18.0.0)");
        Environment.SetEnvironmentVariable("CADDY_HTTPS_PORT", https);
        Environment.SetEnvironmentVariable("CADDY_HTTP_PORT", http);
        Environment.SetEnvironmentVariable("CADDY_HTTPS_HOST", $"localhost:{https}");
        Environment.SetEnvironmentVariable("UMBRACO_PUBLIC_HOST", $"https://localhost:{https}");
        Environment.SetEnvironmentVariable("UMBRACO_PUBLIC_URL", $"https://localhost:{https}/");
        Environment.SetEnvironmentVariable("ARTICULATE_REDIRECT_URI", $"https://localhost:{https}/a-new/");
        Environment.SetEnvironmentVariable("ARTICULATE_LOGOUT_REDIRECT_URI", $"https://localhost:{https}/");
    }

    static async Task ComposeAsync(IEnumerable<string> args, bool allowFailure = false) =>
        await RunAsync("docker", ["compose", .. args], Repo, allowFailure);

    static async Task SmokeAsync(string command)
    {
        var node = Env("NODE_BIN") ?? "node";
        await RunAsync(node, [Path.Combine(DockerDir, "smoke.mjs"), command], Repo);
    }

    static async Task WaitForAsync(Uri uri)
    {
        using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(uri);
                if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Found) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(2000);
        }
        throw new TimeoutException($"Timed out waiting for {uri}.");
    }

    static async Task RunAsync(string file, IEnumerable<string> args, string cwd, bool allowFailure = false)
    {
        Require(file);
        var info = new ProcessStartInfo(file) { WorkingDirectory = cwd, UseShellExecute = false };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        Console.WriteLine($"> {file} {string.Join(' ', info.ArgumentList)}");
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {file}.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0 && !allowFailure)
            throw new InvalidOperationException($"{file} exited with code {process.ExitCode}.");
    }

    static async Task<string> CaptureAsync(string file, IEnumerable<string> args, string cwd)
    {
        Require(file);
        var info = new ProcessStartInfo(file) { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {file}.");
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"{file} exited with code {process.ExitCode}.");
        return output;
    }

    static void DeleteBuildOutputs(string root)
    {
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .Where(path => Path.GetFileName(path) is "bin" or "obj")
                     .OrderByDescending(path => path.Length).ToArray())
            DeleteDirectory(dir);
    }

    static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    static string Lane(Options options)
    {
        var lane = (options.Value("lane") ?? Env("ARTICULATE_PACKAGE_LANE") ?? "v17").ToLowerInvariant();
        if (lane is not ("v17" or "v18")) throw new ArgumentException("Lane must be v17 or v18.");
        return lane;
    }

    static bool BoolOption(Options options, string key, string env, bool fallback) =>
        options.Value(key) is { } value ? IsTrue(value) :
        Env(env) is { } envValue ? IsTrue(envValue) : fallback;

    static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    static string? Env(string name) => Environment.GetEnvironmentVariable(name);
    static void SetDefault(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(Env(name))) Environment.SetEnvironmentVariable(name, value);
    }
    static void RequireSecret()
    {
        if (string.IsNullOrWhiteSpace(Env("ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET")))
            throw new InvalidOperationException("ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET must be set.");
        SetDefault("ARTICULATE_DEV_AUTOMATION_CLIENT_ID", "articulate-dev-automation");
    }
    static void Require(string command)
    {
        if (Path.IsPathRooted(command) && File.Exists(command)) return;
        var names = OperatingSystem.IsWindows() ? new[] { command, $"{command}.exe", $"{command}.cmd" } : [command];
        if ((Env("PATH") ?? "").Split(Path.PathSeparator).Any(dir => names.Any(name => File.Exists(Path.Combine(dir, name))))) return;
        throw new InvalidOperationException($"Required command not found on PATH: {command}");
    }

    static string FindRepo()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "Articulate.Web")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Run this command from the Articulate repository.");
    }
}

sealed class Options
{
    readonly Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);
    public static Options Parse(string[] args)
    {
        var result = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            var key = args[i][2..];
            result.values[key] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : null;
        }
        return result;
    }
    public static Options FromFlags(IEnumerable<string> flags)
    {
        var result = new Options();
        foreach (var flag in flags) result.values[flag] = null;
        return result;
    }
    public static Options FromValues(params (string Key, string Value)[] values)
    {
        var result = new Options();
        foreach (var (key, value) in values) result.values[key] = value;
        return result;
    }
    public string? Value(string key) => values.TryGetValue(key, out var value) ? value : null;
    public bool Flag(string key) => values.ContainsKey(key) && values[key] is null;
}
