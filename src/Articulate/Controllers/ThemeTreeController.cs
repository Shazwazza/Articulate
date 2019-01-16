using System.Net.Http.Formatting;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Core.IO;
using Umbraco.Web.Models.Trees;
using Umbraco.Web.Mvc;
using Umbraco.Web.Trees;
using Umbraco.Web.Actions;

namespace Articulate.Controllers
{
    /// <summary>
    /// Tree for displaying partial views in the settings app
    /// </summary>
    [Tree(Constants.Applications.Settings, "articulatethemes", "Articulate Themes", sortOrder: 100)]
    [PluginController("Articulate")]
    public class ThemeTreeController : FileSystemTreeController
    {
        protected override IFileSystem FileSystem => new PhysicalFileSystem(PathHelper.VirtualThemePath);

        private static readonly string[] ExtensionsStatic = { "cshtml", "js", "css" };

        protected override string[] Extensions => ExtensionsStatic;

        protected override string FileIcon => "icon-article";

        protected override void OnRenderFileNode(ref TreeNode treeNode)
        {
            base.OnRenderFileNode(ref treeNode);           
        }

        protected override MenuItemCollection GetMenuForNode(string id, FormDataCollection queryStrings)
        {
            var menuItemCollection = new MenuItemCollection();
            if (id == Constants.System.Root.ToString())
            {
                menuItemCollection.DefaultMenuAlias = ActionNew.ActionAlias;
                menuItemCollection.Items.Add<ActionNew>(Services.TextService.Localize($"actions/{ActionNew.ActionAlias}"));
                menuItemCollection.Items.Add(new RefreshNode(Services.TextService, true));

                return menuItemCollection;
            }
            var path = string.IsNullOrEmpty(id) || id == Constants.System.Root.ToString() ? "" : HttpUtility.UrlDecode(id).TrimStart("/");
            var dirExists = FileSystem.FileExists(path);
            if (FileSystem.DirectoryExists(path))
            {
                menuItemCollection.DefaultMenuAlias = ActionNew.ActionAlias;
                menuItemCollection.Items.Add<ActionNew>(Services.TextService.Localize($"actions/{ActionNew.ActionAlias}"));                
                menuItemCollection.Items.Add<ActionDelete>(Services.TextService.Localize($"actions/{ActionDelete.ActionAlias}"));
                menuItemCollection.Items.Add(new RefreshNode(Services.TextService, true));
            }
            else if (dirExists)
                menuItemCollection.Items.Add<ActionDelete>(Services.TextService.Localize($"actions/{ActionDelete.ActionAlias}"));
            return menuItemCollection;
        }

        protected override TreeNode CreateRootNode(FormDataCollection queryStrings)
        {
            var node = base.CreateRootNode(queryStrings);
            node.Icon = "icon-voice";
            return node;
        }

        protected override void OnRenderFolderNode(ref TreeNode treeNode)
        {
            //TODO: This isn't the best way to ensure a noop process for clicking a node but it works for now.
            treeNode.AdditionalData["jsClickCallback"] = "javascript:void(0);";

            if (treeNode.ParentId.Equals(string.Empty))
                treeNode.Icon = "icon-layers-alt";
        }
    }
}
