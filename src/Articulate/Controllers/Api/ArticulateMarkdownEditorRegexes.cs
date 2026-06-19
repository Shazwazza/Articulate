#nullable enable
using System.Text.RegularExpressions;

namespace Articulate.Controllers.Api
{
    internal static partial class ArticulateMarkdownEditorRegexes
    {
        // regex finds the image placeholder markdown tag and captures the users label and temporary URL.
        [GeneratedRegex(@"!\[(.*?)\]\((tmp:.*?)\)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled,
            1000)]
        public static partial Regex ImageTagPlaceholderRegex();
    }
}
