using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;
using Umbraco.Web.PropertyEditors.ValueConverters;

namespace Articulate.PropertyEditors
{
    //TODO: INVESTIGATE IF THIS IS FIXED IN V8 CORE - SEEMS STUPID TO HAVE TO HACK THIS

    //NOTE: THis is ONLY overridden because the core markdown editor is nvarchar not ntext!
    [DataEditor("Articulate.MarkdownEditor", "Articulate Markdown editor", "markdowneditor", ValueType = "TEXT")]
    public class ArticulateMarkdownPropertyEditor : MarkdownPropertyEditor
    {
        public ArticulateMarkdownPropertyEditor(ILogger logger) : base(logger)
        {
        }
    }

    //Ugh, this is necessary since we have a custom one - wish we didn't ship this in this version, next major we should remove all of this
    public class ArticulateMarkdownEditorValueConverter : MarkdownEditorValueConverter
    {
        public override bool IsConverter(IPublishedPropertyType propertyType)
            => "Articulate.MarkdownEditor" == propertyType.EditorAlias;
    }
}
