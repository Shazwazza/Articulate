using Umbraco.Cms.Core.PropertyEditors;

namespace Articulate.PropertyEditors
{
    [DataEditor("ArticulateThemePicker", EditorType.PropertyValue, "Articulate Theme Picker", "../App_Plugins/Articulate/BackOffice/PropertyEditors/ThemePicker.html")]
    public class ThemePickerPropertyEditor : DataEditor
    {
        public ThemePickerPropertyEditor(IDataValueEditorFactory dataValueEditorFactory, EditorType type = EditorType.PropertyValue) : base(dataValueEditorFactory, type)
        {
        }
    }
}
