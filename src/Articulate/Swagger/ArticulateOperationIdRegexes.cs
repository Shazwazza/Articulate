#nullable enable
using System.Text.RegularExpressions;

namespace Articulate.Swagger
{
    /// <summary>
    ///     This is the regexes used to generate the operation IDs, the benefit of this being partial with GeneratedRegex
    ///     source generators is that it will be pre-compiled at startup
    ///     See: https://devblogs.microsoft.com/dotnet/regular-expression-improvements-in-dotnet-7/#source-generation for more
    ///     info.
    /// </summary>
    internal static partial class ArticulateOperationIdRegexes
    {
        // Lifted from Umbraco.Cms.Api.Common.OpenApi.OperationIdRegexes
        // Your IDE may be showing errors here, this is because it's a new dotnet 7 feature, error resolved on compile (it's fixed in the EAP of Rider)
        [GeneratedRegex(".*?\\/v[1-9]+/",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex VersionPrefixRegex();

        [GeneratedRegex(@"\{(.*?)\:?\}",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex TemplatePlaceholdersRegex();

        [GeneratedRegex(@"[\/\-](\w{1})",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex ToCamelCaseRegex();
    }
}
