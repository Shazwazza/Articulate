using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;

namespace Articulate.PropertyEditors
{
    [DataEditor("ArticulateThemePicker", EditorType.PropertyValue, "Articulate Theme Picker", "../App_Plugins/Articulate/BackOffice/PropertyEditors/ThemePicker.html")]
    public class ThemePickerPropertyEditor : DataEditor
    {
        public ThemePickerPropertyEditor(ILogger logger) 
            : base(logger)
        {
        }
    }
}
