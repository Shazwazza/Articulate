using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Http;
using System.Web.Http.Routing;
using Articulate.Models;
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
        public Theme PostCopyTheme(string themeName, string copy)
        {
            if (Path.GetInvalidFileNameChars().ContainsAny(themeName.ToCharArray()))
                throw new InvalidOperationException("Name cannot contain invalid file name characters");

            var theme = new DirectoryInfo(Path.Combine(IOHelper.MapPath(PathHelper.VirtualThemePath))).GetDirectories()
                .FirstOrDefault(x => x.Name.InvariantEquals(copy));

            if (theme == null) throw new HttpResponseException(HttpStatusCode.NotFound);

            CopyDirectory(theme, new DirectoryInfo(Path.Combine(IOHelper.MapPath(PathHelper.VirtualThemePath), themeName)));

            return new Theme()
            {
                Name = themeName
            };
        }

        private static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
        {
            if (destination.Exists) throw new InvalidOperationException("Theme already exists");

            destination.Create();

            // Copy all files.
            FileInfo[] files = source.GetFiles();
            foreach (FileInfo file in files)
            {
                file.CopyTo(Path.Combine(destination.FullName,
                    file.Name));
            }

            // Process subdirectories.
            DirectoryInfo[] dirs = source.GetDirectories();
            foreach (DirectoryInfo dir in dirs)
            {
                // Get destination directory.
                string destinationDir = Path.Combine(destination.FullName, dir.Name);

                // Call CopyDirectory() recursively.
                CopyDirectory(dir, new DirectoryInfo(destinationDir));
            }
        }

        public IEnumerable<Theme> GetThemes()
        {
            var themes = new DirectoryInfo(Path.Combine(IOHelper.MapPath(PathHelper.VirtualThemePath))).GetDirectories()
                .Select(x => new Theme
                {
                    Name = x.Name
                });
            return themes;
        }

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