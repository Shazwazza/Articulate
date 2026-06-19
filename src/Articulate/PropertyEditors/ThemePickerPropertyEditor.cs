#nullable enable
using Umbraco.Cms.Core.PropertyEditors;

namespace Articulate.PropertyEditors
{
    // Maps to alias: \Client\src\editors\theme-picker.element.ts
    // ArticulateThemePicker | Umbraco.Plain.String
    /// <summary>
    ///     Property editor for picking Articulate themes.
    /// </summary>
    [DataEditor(
        ArticulateConstants.DataType.ArticulateThemePicker,
        ValueType = ValueTypes.String,
        ValueEditorIsReusable = true)]
    public class ThemePickerPropertyEditor(IDataValueEditorFactory dataValueEditorFactory)
        : DataEditor(dataValueEditorFactory)
    {
        /// <inheritdoc />
        protected override IConfigurationEditor CreateConfigurationEditor() => new ThemePickerConfigurationEditor();
    }
}
