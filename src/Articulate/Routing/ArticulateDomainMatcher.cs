#nullable enable
using Umbraco.Cms.Core.Routing;

namespace Articulate.Routing
{
    internal static class ArticulateDomainMatcher
    {
        public static bool Matches(Domain candidate, Domain currentDomain, Uri? currentUri = null)
        {
            if (candidate.IsWildcard != currentDomain.IsWildcard ||
                !string.Equals(
                    candidate.Culture ?? string.Empty,
                    currentDomain.Culture ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Uri? effectiveCurrentUri = currentUri ?? (currentDomain as DomainAndUri)?.Uri;
            if (effectiveCurrentUri is not null &&
                TryGetComparableUri(candidate, effectiveCurrentUri, out Uri? candidateUri) &&
                TryGetComparableUri(currentDomain, effectiveCurrentUri, out Uri? currentDomainUri))
            {
                return Uri.Compare(
                    candidateUri,
                    currentDomainUri,
                    UriComponents.SchemeAndServer | UriComponents.Path,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0;
            }

            return string.Equals(
                NormalizeName(candidate.Name),
                NormalizeName(currentDomain.Name),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetComparableUri(Domain domain, Uri currentUri, out Uri? comparableUri)
        {
            comparableUri = null;

            try
            {
                comparableUri = domain is DomainAndUri domainAndUri
                    ? domainAndUri.Uri
                    : new DomainAndUri(domain, currentUri).Uri;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string NormalizeName(string? name) => (name ?? string.Empty).Trim().TrimEnd('/');
    }
}
