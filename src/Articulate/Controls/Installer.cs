using System;
using System.Linq;
using System.Web.UI.WebControls;
using Umbraco.Core.Logging;
using Umbraco.Web.UI.Controls;

namespace Articulate.Controls
{
    public class Installer : UmbracoUserControl
    {
        public string BlogUrl { get; private set; }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            var dataInstaller = new ArticulateDataInstaller(Services, Security.CurrentUser.Id);
            var root = dataInstaller.Execute();

            if (root != null)
            {
                BlogUrl = Umbraco.TypedContent(root.Id).Url;
            }
            else
            {
                BlogUrl = "/";
            }
            
        }
    }
}