using System.Net.Http.Formatting;
using AutoMapper;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Web.Models.Trees;
using Umbraco.Web.Mvc;
using Umbraco.Web.Trees;

namespace Articulate.Controllers
{
    /// <summary>
    /// Tree for displaying partial views in the settings app
    /// </summary>
    [Tree(Constants.Applications.Settings, "articulatethemes", "Articulate Themes", sortOrder: 100)]
    [PluginController("Articulate")]
    public class ThemeTreeController : FileSystemTreeController
    {
        protected override IFileSystem2 FileSystem { get; } = new PhysicalFileSystem("~/App_Plugins/Articulate/Themes");

        private static readonly string[] ExtensionsStatic = { "cshtml", "js", "css" };

        protected override string[] Extensions => ExtensionsStatic;

        protected override string FileIcon => "icon-article";

        protected override void OnRenderFileNode(ref TreeNode treeNode)
        {
            base.OnRenderFileNode(ref treeNode);

            
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
