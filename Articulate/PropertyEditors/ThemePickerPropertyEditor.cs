using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.PropertyEditors;

namespace Articulate.PropertyEditors
{
    [PropertyEditor("ArticulateThemePicker", "Articulate Theme Picker", "../App_Plugins/Articulate/PropertyEditors/ThemePicker.html")]
    public class ThemePickerPropertyEditor : PropertyEditor
    {
    }
}
