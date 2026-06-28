using Umbraco.Cms.Core.Models.PublishedContent;
using Articulate.Options;

namespace Articulate.Models
{
    /// <summary>
    /// Comment-provider concern of <see cref="MasterModel"/>: Disqus + Giscus
    /// configuration, resolution, and enablement predicates.
    /// </summary>
    public partial class MasterModel
    {
        public string DisqusShortName
        {
            get => field ??= Unwrap().Value<string>("disqusShortname", fallback: Fallback.ToAncestors);
            protected set;
        }

        private bool? _isDisqusEnabled;

        /// <summary>
        /// Gets whether Disqus comments are enabled and configured with a valid shortname.
        /// Validates that the DisqusShortName is not empty and contains only valid characters (alphanumeric and hyphens).
        /// </summary>
        public bool IsDisqusEnabled
        {
            get
            {
                _isDisqusEnabled ??= !string.IsNullOrWhiteSpace(DisqusShortName)
                    && IsValidDisqusShortName(DisqusShortName);
                return _isDisqusEnabled.Value;
            }
        }

        private ArticulateConstants.Comments.Provider? _commentsProvider;

        public ArticulateConstants.Comments.Provider CommentsProvider
        {
            get
            {
                _commentsProvider ??= ResolveProvider(IsDisqusEnabled, IsGiscusEnabled);
                return _commentsProvider.Value;
            }
        }

        public bool IsCommentsEnabled => CommentsProvider != ArticulateConstants.Comments.Provider.None;

        public string GiscusScriptSrc
            => field ??= CommentsOptions.Giscus.ScriptSrc;

        private (string Repo, string RepoId, string Category, string CategoryId)? _giscusRequired;

        private (string Repo, string RepoId, string Category, string CategoryId) ResolvedGiscusRequired
        {
            get
            {
                return _giscusRequired ??= ResolveGiscusRequired(
                    Unwrap().Value<string>("giscusRepo", fallback: Fallback.ToAncestors),
                    Unwrap().Value<string>("giscusRepoId", fallback: Fallback.ToAncestors),
                    Unwrap().Value<string>("giscusCategory", fallback: Fallback.ToAncestors),
                    Unwrap().Value<string>("giscusCategoryId", fallback: Fallback.ToAncestors),
                    CommentsOptions.Giscus);
            }
        }

        public string GiscusRepo
            => field ??= ResolvedGiscusRequired.Repo;

        public string GiscusRepoId
            => field ??= ResolvedGiscusRequired.RepoId;

        public string GiscusCategory
            => field ??= ResolvedGiscusRequired.Category;

        public string GiscusCategoryId
            => field ??= ResolvedGiscusRequired.CategoryId;

        public string GiscusMapping => CommentsOptions.Giscus.DataMapping;

        public string GiscusStrict => CommentsOptions.Giscus.DataStrict;

        public string GiscusReactionsEnabled => CommentsOptions.Giscus.DataReactionsEnabled;

        public string GiscusEmitMetadata => CommentsOptions.Giscus.DataEmitMetadata;

        public string GiscusInputPosition => CommentsOptions.Giscus.DataInputPosition;

        public string GiscusTheme => CommentsOptions.Giscus.DataTheme;

        public string GiscusLang => CommentsOptions.Giscus.DataLang;

        public string GiscusLoading => CommentsOptions.Giscus.DataLoading;

        public bool IsGiscusEnabled =>
            !string.IsNullOrWhiteSpace(GiscusRepo) &&
            !string.IsNullOrWhiteSpace(GiscusRepoId) &&
            !string.IsNullOrWhiteSpace(GiscusCategory) &&
            !string.IsNullOrWhiteSpace(GiscusCategoryId);

        internal static ArticulateConstants.Comments.Provider ResolveProvider(bool disqusShortNameSet, bool giscusConfigured)
        {
            if (disqusShortNameSet)
            {
                return ArticulateConstants.Comments.Provider.Disqus;
            }

            return giscusConfigured ? ArticulateConstants.Comments.Provider.Giscus : ArticulateConstants.Comments.Provider.None;
        }

        /// <summary>
        /// Resolves the four required Giscus fields (repo, repo id, category, category id)
        /// as an all-or-nothing override: if all four doc-type values are populated they win,
        /// otherwise the appsettings values are used for all four. A partial override is
        /// discarded to avoid mixing doc-type and appsettings values into a configuration
        /// that giscus.app silently rejects.
        /// Pure function, internal static so it's testable without an Umbraco instance.
        /// </summary>
        internal static (string Repo, string RepoId, string Category, string CategoryId) ResolveGiscusRequired(
            string docRepo,
            string docRepoId,
            string docCategory,
            string docCategoryId,
            GiscusCommentsOptions appsettings)
        {
            bool allSet = !string.IsNullOrWhiteSpace(docRepo)
                       && !string.IsNullOrWhiteSpace(docRepoId)
                       && !string.IsNullOrWhiteSpace(docCategory)
                       && !string.IsNullOrWhiteSpace(docCategoryId);
            return allSet
                ? (docRepo, docRepoId, docCategory, docCategoryId)
                : (appsettings.DataRepo, appsettings.DataRepoId, appsettings.DataCategory, appsettings.DataCategoryId);
        }

        private static bool IsValidDisqusShortName(ReadOnlySpan<char> shortName)
        {
            foreach (var c in shortName)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        public ArticulateCommentsOptions CommentsOptions { get; }

    }
}
