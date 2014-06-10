using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;

namespace Articulate.PropertyEditors
{
    //NOTE: THis is ONLY overridden because the core markdown editor is nvarchar not ntext!
    [PropertyEditor("Articulate.MarkdownEditor", "Articulate Markdown editor", "markdowneditor", ValueType = "TEXT")]
    public class ArticulateMarkdownPropertyEditor : MarkdownPropertyEditor
    {
    }
}