#nullable enable

namespace Articulate.Options
{
    /// <summary>
    /// Outcome of <see cref="GiscusCommentsOptions.ResolveCorsHeaders"/>. <c>AllowOrigin</c>
    /// null means "do not emit an <c>Access-Control-Allow-Origin</c> header" (the browser
    /// will block the response). <c>Vary</c> is always true because the headers depend on
    /// the request <c>Origin</c>, so shared caches cannot serve one origin's response
    /// to another origin.
    /// </summary>
    internal sealed record CorsHeaderDecision(string? AllowOrigin, bool Vary);
}
