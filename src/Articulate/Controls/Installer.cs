//using System;
//using System.Linq;
//using System.Web.UI.WebControls;
//using Umbraco.Core.Logging;
//using Umbraco.Web.UI.Controls;

//namespace Articulate.Controls
//{
//    public class Installer : UmbracoUserControl
//    {
//        public string BlogUrl { get; private set; }

//        protected override void OnInit(EventArgs e)
//        {
//            base.OnInit(e);

//            var dataInstaller = new ArticulateDataInstaller(Security.CurrentUser.Id);
//            var root = dataInstaller.Execute(out bool packageInstalled);

//            if (root != null)
//            {
//                BlogUrl = Umbraco.Content(root.Id).Url;
//            }
//            else
//            {
//                BlogUrl = "/";
//            }
            
//        }
//    }
//}