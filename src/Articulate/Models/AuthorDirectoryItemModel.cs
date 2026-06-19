#nullable enable
using Umbraco.Cms.Core.Models;

namespace Articulate.Models
{
    /// <summary>
    ///     Model for rendering an author item in the authors directory.
    /// </summary>
    public class AuthorDirectoryItemModel
    {
        public string Name { get; init; } = string.Empty;

        public string Bio { get; init; } = string.Empty;

        public string? AuthorUrl { get; init; }

        public string BlogUrl { get; init; } = string.Empty;

        public string AuthorRssUrl { get; init; } = string.Empty;

        public MediaWithCrops? Image { get; init; }

        public string CroppedWideUrl { get; init; } = string.Empty;

        public int PostCount { get; init; }

        public DateTime? LastPostDate { get; init; }
    }
}
