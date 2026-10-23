#nullable enable

namespace Articulate.Options
{
    /// <summary>
    /// Comment provider settings configured from appsettings.
    /// </summary>
    public class ArticulateCommentsOptions
    {
        /// <summary>
        /// Gets or sets Giscus comment settings.
        /// </summary>
        public GiscusCommentsOptions Giscus { get; set; } = new();
    }
}
