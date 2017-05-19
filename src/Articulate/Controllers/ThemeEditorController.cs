using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Http;
using System.Web.Http.Routing;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Web.Editors;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi.Filters;

namespace Articulate.Controllers
{
    [PluginController("Articulate")]    
    [UmbracoApplicationAuthorize(Constants.Applications.Settings)]
    public class ThemeEditorController : BackOfficeNotificationsController
    {        
        public CodeFileDisplay GetByPath(string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(virtualPath));

            virtualPath = HttpUtility.UrlDecode(virtualPath);

            var themeFile = new FileInfo(Path.Combine(IOHelper.MapPath(PathHelper.VirtualThemePath), virtualPath.Replace("/", "\\")));

            //var script = Services.FileService.GetScriptByName(virtualPath);
            if (themeFile.Exists)
            {
                using (var reader = themeFile.OpenText())
                {
                    var display = new CodeFileDisplay
                    {
                        Content = reader.ReadToEnd(),
                        FileType = themeFile.Extension,
                        Id = HttpUtility.UrlEncode(virtualPath),
                        Name = themeFile.Name,
                        Path = GetTreePathFromFilePath(Url, virtualPath),
                        VirtualPath = PathHelper.ThemePath.EnsureEndsWith('/') + virtualPath
                    };
                    display.FileType = "ArticulateThemeFile";
                    return display;
                }
               
            }
            throw new HttpResponseException(HttpStatusCode.NotFound);
        }

        internal static string GetTreePathFromFilePath(UrlHelper urlHelper, string virtualPath, string basePath = "")
        {
            //This reuses the Logic from umbraco.cms.helpers.DeepLink class
            //to convert a filepath to a tree syncing path string. 

            //removes the basepath from the path 
            //and normalises paths - / is used consistently between trees and editors
            basePath = basePath.TrimStart("~");
            virtualPath = virtualPath.TrimStart("~");
            virtualPath = virtualPath.Substring(basePath.Length);
            virtualPath = virtualPath.Replace('\\', '/');

            //-1 is the default root id for trees
            var sb = new StringBuilder("-1");

            //split the virtual path and iterate through it
            var pathPaths = virtualPath.Split('/');

            for (var p = 0; p < pathPaths.Length; p++)
            {
                var path = HttpUtility.UrlEncode(string.Join("/", pathPaths.Take(p + 1)));
                if (string.IsNullOrEmpty(path) == false)
                {
                    sb.Append(",");
                    sb.Append(path);
                }
            }
            return sb.ToString().TrimEnd(",");
        }

    }
}