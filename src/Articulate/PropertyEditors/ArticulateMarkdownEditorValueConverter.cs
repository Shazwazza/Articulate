#nullable enable
using Articulate.Services;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Templates;
using UmbracoMarkdownConverter = Umbraco.Cms.Core.Strings.IMarkdownToHtmlConverter;

namespace Articulate.PropertyEditors
{
    // Full clone of src/Umbraco.Web.UI.Client/src/packages/markdown-editor
    // Prevent conflicts if both Markdown Editors on content type
    // Keep this clone while supported Umbraco versions still expose the legacy Markdown editor shape.
    /// <summary>
    /// Value converter for the Articulate Markdown editor.
    /// </summary>
    public class ArticulateMarkdownEditorValueConverter(
        HtmlLocalLinkParser localLinkParser,
        HtmlUrlParser urlParser,
        UmbracoMarkdownConverter umbracoMarkdownConverter,
        IArticulateMarkdownConverter articulateMarkdownConverter)
        : MarkdownEditorValueConverter(localLinkParser, urlParser, umbracoMarkdownConverter)
    {
        /// <inheritdoc/>
        public override bool IsConverter(IPublishedPropertyType propertyType)

            // Maps to alias: \Client\src\packages\articulate-markdown-editor\property-editors\markdown-editor\Articulate.MarkdownEditor.ts
            => propertyType.EditorUiAlias.Equals(ArticulateConstants.DataType.ArticulateMarkdownEditor) ||
               propertyType.EditorAlias.Equals(ArticulateConstants.DataType.ArticulateMarkdownEditor);

        /// <inheritdoc/>
        public override object ConvertIntermediateToObject(
            IPublishedElement owner,
            IPublishedPropertyType propertyType,
            PropertyCacheLevel referenceCacheLevel,
            object? inter,
            bool preview)
        {
            var md = inter as string;
            return new HtmlEncodedString(inter is null
                ? string.Empty
                : articulateMarkdownConverter.ToHtml(md ?? string.Empty));
        }
    }
}
