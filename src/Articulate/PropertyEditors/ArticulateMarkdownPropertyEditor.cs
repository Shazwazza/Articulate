using Markdig;
using System.Web;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;
using Umbraco.Web.PropertyEditors.ValueConverters;

namespace Articulate.PropertyEditors
{

    [DataEditor("Articulate.MarkdownEditor", "Articulate Markdown editor", "markdowneditor", ValueType = "TEXT")]
    public class ArticulateMarkdownPropertyEditor : MarkdownPropertyEditor
    {
        public ArticulateMarkdownPropertyEditor(ILogger logger) : base(logger)
        {
        }
    }

    // using a reasonable Markdown converter
    public class ArticulateMarkdownEditorValueConverter : MarkdownEditorValueConverter
    {
        public override bool IsConverter(IPublishedPropertyType propertyType)
            => "Articulate.MarkdownEditor" == propertyType.EditorAlias;

        public override object ConvertIntermediateToObject(
            IPublishedElement owner,
            IPublishedPropertyType propertyType,
            PropertyCacheLevel referenceCacheLevel,
            object inter,
            bool preview)
        {
            var md = (string)inter;
            return new HtmlString((inter == null) ? string.Empty : MarkdownHelper.ToHtml(md));
        }
    }
}
