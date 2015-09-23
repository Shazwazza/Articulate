using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors.ValueConverters;

namespace Articulate.PropertyEditors
{
    [PropertyEditor("ArticulateThemePicker", "Articulate Theme Picker", "../App_Plugins/Articulate/BackOffice/PropertyEditors/ThemePicker.html")]
    public class ThemePickerPropertyEditor : PropertyEditor
    {
    }
}
