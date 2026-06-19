#nullable enable
using Umbraco.Cms.Core.Models;

namespace Articulate.Models
{
    /// <summary>
    ///     Model for a post author.
    /// </summary>
    public class PostAuthorModel
    {
        public string? Name { get; set; }

        public string? Bio { get; set; }

        public string? Url { get; set; }

        public MediaWithCrops? Image { get; set; }

        public string? BlogUrl { get; set; }
    }
}
