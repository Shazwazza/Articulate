using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;

namespace Articulate.PropertyEditors
{
    [DataEditor("ArticulateThemePicker", "Articulate Theme Picker", "../App_Plugins/Articulate/BackOffice/PropertyEditors/ThemePicker.html")]
    public class ThemePickerPropertyEditor : DataEditor
    {
        public ThemePickerPropertyEditor(ILogger logger, EditorType type = EditorType.PropertyValue) : base(logger, type)
        {
        }
    }
}
