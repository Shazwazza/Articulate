#!/usr/bin/env -S dotnet --
#:property NoWarn=SA1400,SA1503,SA1519,SA1116,SA1117,SA1122,SA1649,IDE0008,IDE0011,IDE0040,SA1500
#nullable enable
// giscus-migrate.cs — migrate the Articulate Disqus XML comment export to GitHub Discussions (giscus).
// Reads the wp:comment RSS shape produced by the BlogML importer's "Export Disqus Xml" option (and native
// Disqus admin exports of the same shape). Creates one discussion per post and one reply per comment.
// Author emails in the export are ignored on purpose (privacy). Dry-run by default; --go writes for real.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

const string WpNamespace = "http://wordpress.org/export/1.0/";
const string CreateMutation = """
    mutation($r:ID!,$c:ID!,$t:String!,$b:String!){
      createDiscussion(input:{repositoryId:$r, categoryId:$c, title:$t, body:$b}){
        discussion{ number }
      }
    }
    """;
const string NodeQuery = """
    query($o:String!,$n:String!,$num:Int!){
      repository(owner:$o, name:$n){ discussion(number:$num){ id } }
    }
    """;
const string ReplyMutation = """
    mutation($d:ID!,$b:String!){
      addDiscussionComment(input:{discussionId:$d, body:$b}){ comment{ id } }
    }
    """;
const string ListQuery = """
    query($o:String!,$n:String!,$c:ID!,$after:String){
      repository(owner:$o, name:$n){
        discussions(first:100, after:$after, categoryId:$c){
          nodes{ id title comments{ totalCount } }
          pageInfo{ hasNextPage endCursor }
        }
      }
    }
    """;

if (args.Length == 0 || args[0] is "-h" or "--help") { Help(); return 0; }

string? file = null, repo = null, repoId = null, categoryId = null, titleSuffix = null, site = null, mapping = "title";
bool go = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--go": go = true; break;
        case "--repo": repo = args[++i]; break;
        case "--repo-id": repoId = args[++i]; break;
        case "--category-id": categoryId = args[++i]; break;
        case "--title-suffix": titleSuffix = args[++i]; break;
        case "--mapping": mapping = args[++i]; break;
        case "--site": site = args[++i].TrimEnd('/'); break;
        default:
            if (args[i].StartsWith('-'))
            {
                Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                Help();
                return 1;
            }
            if (file is not null)
            {
                Console.Error.WriteLine($"Unexpected second file '{args[i]}' (already: '{file}').");
                Help();
                return 1;
            }
            file = args[i];
            break;
    }
}

if (file is null) { Console.Error.WriteLine("Missing export file path."); Help(); return 1; }
if (mapping is not ("title" or "pathname"))
{
    Console.Error.WriteLine("--mapping must be 'title' or 'pathname'.");
    return 1;
}
if (mapping == "pathname" && site is null)
{
    Console.Error.WriteLine("--mapping pathname requires --site <origin>.");
    return 1;
}
if (mapping == "pathname" && titleSuffix is not null)
{
    Console.Error.WriteLine("--title-suffix cannot be used with --mapping pathname.");
    return 1;
}
if (go && (repo is null || repoId is null || categoryId is null))
{
    Console.Error.WriteLine("--go requires --repo <owner/name>, --repo-id and --category-id (the giscus data-repo-id / data-category-id values).");
    return 1;
}

// --- parse the export ---
var xml = XDocument.Load(file);
var posts = xml
    .Descendants("item")
    .Select(item => new
    {
        Title = (string?)item.Element("title") ?? "",
        Comments = item.Elements(XName.Get("comment", WpNamespace))
            .Select(c => new
            {
                Date = ((string?)c.Element(XName.Get("comment_date_gmt", WpNamespace)))?.Trim() ?? "",
                Author = ((string?)c.Element(XName.Get("comment_author", WpNamespace)))?.Trim() ?? "unknown",
                Body = StripHtml(((string?)c.Element(XName.Get("comment_content", WpNamespace))) ?? ""),
            })
            .Where(c => c.Body.Length > 0)
            .OrderBy(c => c.Date)
            .ToList(),
    })
    .Where(p => p.Title.Length > 0)
    .ToList();

var totalComments = posts.Sum(p => p.Comments.Count);
Console.WriteLine($"posts: {posts.Count}, comments: {totalComments}");
Console.WriteLine($"mode: {(go ? "LIVE (gh api graphql)" : "DRY RUN (no calls)")}\n");

// --- optional URL map from a live site's Delivery API (title -> route path) ---
Dictionary<string, string>? urlMap = null;
if (site is not null)
{
    Console.WriteLine($"Fetching published routes from {site} …");
    using var handler = new HttpClientHandler();
    if (new Uri(site).HostNameType == UriHostNameType.Dns &&
        (site.Contains("localhost") || site.Contains("127.0.0.1")))
    {
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true; // dev stacks use untrusted certs
    }

    using var client = new HttpClient(handler);
    var json = await client.GetStringAsync($"{site}/umbraco/delivery/api/v2/content?pageSize=100");
    using var doc = JsonDocument.Parse(json);
    urlMap = [];
    foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
    {
        var title = item.GetProperty("title").GetString();
        var path = item.GetProperty("route").GetProperty("path").GetString();
        if (title is not null && path is not null) urlMap[title] = path;
    }
    Console.WriteLine($"url map: {urlMap.Count} published routes.\n");
}

// --- resume support: skip finished discussions, continue partly-replied ones ---
var existing = new Dictionary<string, (string Id, int Replies)>(StringComparer.Ordinal);
if (go && repo is not null)
{
    var parts = repo.Split('/');
    string? cursor = null;
    while (true)
    {
        var listVars = new Dictionary<string, string> { ["o"] = parts[0], ["n"] = parts[1], ["c"] = categoryId! };
        if (cursor is not null) listVars["after"] = cursor;
        var listed = await GhGraphQL(ListQuery, listVars, "listing existing discussions", typed: false, pace: 0);
        var page = listed.GetProperty("data").GetProperty("repository").GetProperty("discussions");
        foreach (var node in page.GetProperty("nodes").EnumerateArray())
            existing[node.GetProperty("title").GetString() ?? ""] = (node.GetProperty("id").GetString()!, node.GetProperty("comments").GetProperty("totalCount").GetInt32());
        var pageInfo = page.GetProperty("pageInfo");
        if (!pageInfo.GetProperty("hasNextPage").GetBoolean()) break;
        cursor = pageInfo.GetProperty("endCursor").GetString();
    }
    if (existing.Count > 0) Console.WriteLine($"resuming: {existing.Count} discussions already exist; finished ones are skipped, partial ones continue.\n");
}

// --- migrate ---
foreach (var post in posts)
{
    var path = urlMap?.GetValueOrDefault(post.Title);
    if (mapping == "pathname" && path is null)
    {
        Console.Error.WriteLine($"No published route found for '{post.Title}'.");
        return 1;
    }

    var discussionTitle = mapping == "pathname" ? path! : post.Title + titleSuffix;
    var found = existing.TryGetValue(discussionTitle, out var prior);
    if (found && prior.Replies >= post.Comments.Count) { Console.WriteLine($"• skip (exists): {post.Title}"); continue; }

    var body = $"Migrated comments for: **{post.Title}** ({post.Comments.Count} comment{(post.Comments.Count == 1 ? "" : "s")})";
    if (path is not null) body += $"\n\nURL: {site}{path}";

    Console.WriteLine($"• {discussionTitle}  [{post.Comments.Count} comments]");
    if (post.Comments.Count > 0)
        Console.WriteLine($"    first: {post.Comments[0].Author} ({post.Comments[0].Date}): {Truncate(post.Comments[0].Body, 90)}");

    if (!go) continue;

    string? discussionId;
    var skipReplies = 0;
    if (found)
    {
        // shortcut: assumes earlier replies were posted in order and none were deleted, upgrade if edited discussions must resume
        discussionId = prior.Id;
        skipReplies = prior.Replies;
        Console.WriteLine($"    resuming discussion with {skipReplies} replies already posted");
    }
    else
    {
        var createVars = new Dictionary<string, string> { ["r"] = repoId!, ["c"] = categoryId!, ["t"] = discussionTitle, ["b"] = body };
        var created = await GhGraphQL(CreateMutation, createVars, $"creating discussion \"{discussionTitle}\"", typed: false, pace: 0);
        var number = created.GetProperty("data").GetProperty("createDiscussion").GetProperty("discussion").GetProperty("number").GetInt32();

        // GitHub's createDiscussion can return a stale node id; re-query by number for the truth.
        var parts = repo!.Split('/');
        var nodeVars = new Dictionary<string, string> { ["o"] = parts[0], ["n"] = parts[1], ["num"] = number.ToString() };
        var nodeDoc = await GhGraphQL(NodeQuery, nodeVars, $"resolving node id for \"{discussionTitle}\"", typed: true, pace: 0);
        discussionId = nodeDoc.GetProperty("data").GetProperty("repository").GetProperty("discussion").GetProperty("id").GetString();
        Console.WriteLine($"    created discussion #{number}");
    }

    foreach (var comment in post.Comments.Skip(skipReplies))
    {
        var replyBody = $"_Originally by **{comment.Author}**, {comment.Date}_\n\n{comment.Body}";
        var replyVars = new Dictionary<string, string> { ["d"] = discussionId!, ["b"] = replyBody };
        await GhGraphQL(ReplyMutation, replyVars, $"replying on \"{discussionTitle}\"", typed: false, pace: 1500);
        await Task.Delay(1500); // github anti-spam: rapid discussion comments get rejected
    }
}

Console.WriteLine(go ? $"\ndone: {posts.Count} discussions, {totalComments} replies processed." : $"\ndry run: would create {posts.Count} discussions + {totalComments} replies.");
Console.WriteLine(go ? "" : "re-run with --go --repo <owner/name> --repo-id <id> --category-id <id> to execute.");
return 0;

static void Help()
{
    Console.WriteLine("""
        disqus-migrate — migrate Articulate's Disqus/RSS comment export to GitHub Discussions (giscus).

        Usage:
          dotnet run --file build/giscus-migrate.cs -- <export.xml> [options]

        Options:
          --go                 Execute for real. Without it the tool prints the plan and calls nothing.
          --repo <owner/name>  Target repository. Required with --go.
          --repo-id <id>       Repository GraphQL node id (giscus data-repo-id). Required with --go.
          --category-id <id>   Discussion category GraphQL node id (giscus data-category-id). Required with --go.
          --mapping <mode>     Discussion key: title (default) or pathname.
                               pathname requires --site and creates discussions titled with the route.
          --title-suffix <s>   With title mapping, append this to each title so it matches the site's
                               "<post> - <blog>" page title.
          --site <origin>      Fetch published routes from this origin's Delivery API. Required for
                               pathname mapping; also adds each live URL to title-mapped discussions.

        Notes:
          - Dry run by default; nothing is created unless --go is passed.
          - Author emails in the export are never read or migrated.
          - Discussion comments are paced and retried to stay under GitHub's anti-spam limits;
            the run is resumable (finished discussions are skipped, partial ones continue).
        """);
}

static string StripHtml(string html)
{
    var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
    text = System.Net.WebUtility.HtmlDecode(text);
    return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
}

static string Truncate(string value, int length)
    => value.Length <= length ? value : value[..length] + "…";

static async Task<JsonElement> GhGraphQL(
    string mutation,
    IReadOnlyDictionary<string, string> vars,
    string context,
    bool typed,
    int pace)
{
    const int maxAttempts = 6;
    int[] waits = [10000, 30000, 60000, 120000, 300000, 600000];

    for (var attempt = 0; ; attempt++)
    {
        if (pace > 0) await Task.Delay(pace);

        var psi = new ProcessStartInfo("gh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("api");
        psi.ArgumentList.Add("graphql");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add($"query={mutation}");
        foreach (var (key, value) in vars)
            psi.ArgumentList.Add(typed ? $"-F{key}={value}" : $"-f{key}={value}");

        Console.WriteLine($"> gh api graphql ({context})");
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start gh.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var rateLimited = error.Contains("submitted too quickly", StringComparison.OrdinalIgnoreCase);
            if (rateLimited && attempt < maxAttempts - 1)
            {
                var wait = waits[Math.Min(attempt, waits.Length - 1)];
                Console.WriteLine($"    rate limited, waiting {wait / 1000}s…");
                await Task.Delay(wait);
                continue;
            }

            throw new InvalidOperationException($"gh api graphql failed while {context}: {error.Trim()}");
        }

        using var parsed = JsonDocument.Parse(output);
        return parsed.RootElement.Clone();
    }
}
