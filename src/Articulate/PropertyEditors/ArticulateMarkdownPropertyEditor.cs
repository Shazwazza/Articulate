using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;

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
}