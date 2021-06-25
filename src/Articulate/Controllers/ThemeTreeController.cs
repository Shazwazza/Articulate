using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models.Trees;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Trees;
using Umbraco.Cms.Web.BackOffice.Trees;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Extensions;

namespace Articulate.Controllers
{
    /// <summary>
    /// Tree for displaying partial views in the settings app
    /// </summary>
    [Tree(Constants.Applications.Settings, "articulatethemes", TreeTitle = "Articulate Themes", SortOrder = 100)]
    [PluginController("Articulate")]
    public class ThemeTreeController : FileSystemTreeController
    {
        private static readonly string[] s_extensionsStatic = { "cshtml", "js", "css" };
        private readonly IFileSystem _fileSystem;

        public ThemeTreeController(
            ILocalizedTextService localizedTextService,
            UmbracoApiControllerTypeCollection umbracoApiControllerTypeCollection,
            IMenuItemCollectionFactory menuItemCollectionFactory,
            IEventAggregator eventAggregator,
            IIOHelper ioHelper,
            IHostingEnvironment hostingEnvironment,
            ILoggerFactory loggerFactory)
            : base(localizedTextService, umbracoApiControllerTypeCollection, menuItemCollectionFactory, eventAggregator)
            => _fileSystem = new PhysicalFileSystem(
                ioHelper,
                hostingEnvironment,
                loggerFactory.CreateLogger<PhysicalFileSystem>(),
                hostingEnvironment.MapPathContentRoot(PathHelper.VirtualThemePath),
                hostingEnvironment.ToAbsolute(PathHelper.VirtualThemePath));

        protected override IFileSystem FileSystem => _fileSystem;

        protected override string[] Extensions => s_extensionsStatic;

        protected override string FileIcon => "icon-article";

        protected override void OnRenderFileNode(ref TreeNode treeNode)
        {
            base.OnRenderFileNode(ref treeNode);
        }

        protected override ActionResult<MenuItemCollection> GetMenuForNode(string id, FormCollection queryStrings)
        {
            MenuItemCollection menuItemCollection = MenuItemCollectionFactory.Create();
            if (id == Constants.System.Root.ToString())
            {
                menuItemCollection.DefaultMenuAlias = ActionNew.ActionAlias;
                menuItemCollection.Items.Add<ActionNew>(LocalizedTextService);
                menuItemCollection.Items.Add(new RefreshNode(LocalizedTextService, true));

                return menuItemCollection;
            }
            var path = string.IsNullOrEmpty(id) || id == Constants.System.Root.ToString() ? "" : HttpUtility.UrlDecode(id).TrimStart("/");
            var dirExists = FileSystem.FileExists(path);
            if (FileSystem.DirectoryExists(path))
            {
                menuItemCollection.DefaultMenuAlias = ActionNew.ActionAlias;
                menuItemCollection.Items.Add<ActionNew>(LocalizedTextService);
                menuItemCollection.Items.Add<ActionDelete>(LocalizedTextService);
                menuItemCollection.Items.Add(new RefreshNode(LocalizedTextService, true));
            }
            else if (dirExists)
            {
                menuItemCollection.Items.Add<ActionDelete>(LocalizedTextService);
            }

            return menuItemCollection;
        }

        protected override ActionResult<TreeNode> CreateRootNode(FormCollection queryStrings)
        {
            ActionResult<TreeNode> node = base.CreateRootNode(queryStrings);
            node.Value.Icon = "icon-voice";
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
