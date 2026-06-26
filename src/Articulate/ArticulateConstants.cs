#nullable enable
namespace Articulate
{
    /// <summary>
    /// Constants used throughout the Articulate project.
    /// </summary>
    public static class ArticulateConstants
    {
        internal const string RefreshRoutesToken = "articulate-refresh-routes";

        /// <summary>
        /// Content type aliases.
        /// </summary>
        public static class ContentType
        {
            public const string Articulate = "Articulate";
            public const string ArticulateArchive = "ArticulateArchive";
            public const string ArticulateAuthor = "ArticulateAuthor";
            public const string ArticulateAuthors = "ArticulateAuthors";
            public const string ArticulateMarkdown = "ArticulateMarkdown";
            public const string ArticulatePost = "ArticulatePost";
            public const string ArticulateRichText = "ArticulateRichText";
        }

        /// <summary>
        /// Comment provider and configuration identifiers.
        /// </summary>
        public static class Comments
        {
            /// <summary>
            /// The comment provider resolved for a post. <see cref="Provider.None"/> means
            /// no provider is configured for the current blog (no Disqus shortname and no
            /// fully-populated Giscus config), in which case the comments partial renders nothing.
            /// </summary>
            public enum Provider
            {
                None = 0,
                Disqus = 1,
                Giscus = 2,
            }
        }

        /// <summary>
        /// Naming conventions and document aliases.
        /// </summary>
        public static class Convention
        {
            public const string ArticlesDocument = "Articles";
            public const string ArticulateMediaFolder = "Articulate";
            public const string AuthorsDocument = "Authors";
        }

        /// <summary>
        /// Data type aliases and keys.
        /// </summary>
        public static class DataType
        {
            public const string ArticulateCategories = "ArticulateCategories";
            public const string ArticulateMarkdownEditor = "Articulate.MarkdownEditor";
            public const string ArticulateTags = "ArticulateTags";
            public const string ArticulateThemePicker = "ArticulateThemePicker";

            public static readonly Guid ArticulateRichTextKey = new("DBCB0707-021D-4CD4-BA8B-5CC891516C28");
        }

        /// <summary>
        /// Default theme names.
        /// </summary>
        public static class DefaultThemes
        {
            /// <summary>
            /// Gets all built-in theme names.
            /// </summary>
            public static IReadOnlyList<string> AllThemeNames { get; } = Array.AsReadOnly([Vapor, Material, Phantom, Mini]);

            private const string Material = "Material";
            private const string Mini = "Mini";
            private const string Phantom = "Phantom";
            private const string Vapor = "VAPOR";
        }

        /// <summary>
        /// Migration plan and step names.
        /// </summary>
        public static class Migration
        {
            public const string AutomaticPackageMigrationPlan = "Articulate";
            public const string ArticulatePackageMigrationPlan = "Articulate.Core";
        }

        internal static class Paths
        {
            internal const string ArticulateTemp = "Articulate/Temp";
            internal const string ArticulateRoot = "App_Plugins/Articulate";
            internal const string Themes = "Themes";
            internal const string Views = "Views";
            internal const string Partials = "Partials";
            internal const string MarkdownEditor = "MarkdownEditor";

            internal const string ViewPlaceholder = "{0}.cshtml";

            internal const string UserThemesRoot = "Views/ArticulateThemes";
        }

        /// <summary>
        /// Management API names and groups.
        /// </summary>
        public static class ManagementApi
        {
            public const string Name = "articulate";

            public const string BlogMl = "BlogML";
            public const string MarkdownEditor = "Markdown Editor";
            public const string ThemePicker = "Theme Picker";
            public const string ThemeOptions = "Theme Options";
        }
    }
}
