#nullable enable
using Articulate.Options;

namespace Articulate.Models
{
    /// <summary>
    /// The comment-provider context for an Articulate page: Disqus and Giscus
    /// configuration, the resolved provider, and enablement predicates.
    /// Segregated from <see cref="IMasterModel"/> so comment rendering depends
    /// only on this contract, keeping the blog-identity/theme/RSS core of
    /// <see cref="IMasterModel"/> decoupled from comment-provider config.
    /// </summary>
    public interface ICommentsContext
    {
        /// <summary>
        /// Gets the Disqus short name.
        /// </summary>
        public string DisqusShortName { get; }

        /// <summary>
        /// Gets the comment options (appsettings defaults) resolved for this model.
        /// Per-blog overrides still come from the document via <c>Fallback.ToAncestors</c>;
        /// this carries only the global appsettings fallback values.
        /// </summary>
        public ArticulateCommentsOptions CommentsOptions { get; }

        /// <summary>
        /// Gets whether Disqus comments are enabled and configured with a valid shortname.
        /// </summary>
        public bool IsDisqusEnabled { get; }

        /// <summary>
        /// Gets the resolved comment provider.
        /// </summary>
        public ArticulateConstants.Comments.Provider CommentsProvider { get; }

        /// <summary>
        /// Gets whether any comment provider is configured.
        /// </summary>
        public bool IsCommentsEnabled { get; }

        public string GiscusScriptSrc { get; }

        public string GiscusRepo { get; }

        public string GiscusRepoId { get; }

        public string GiscusCategory { get; }

        public string GiscusCategoryId { get; }

        public string GiscusMapping { get; }

        public string GiscusStrict { get; }

        public string GiscusReactionsEnabled { get; }

        public string GiscusEmitMetadata { get; }

        public string GiscusInputPosition { get; }

        public string GiscusTheme { get; }

        public string GiscusLang { get; }

        public string GiscusLoading { get; }

        public bool IsGiscusEnabled { get; }
    }
}
