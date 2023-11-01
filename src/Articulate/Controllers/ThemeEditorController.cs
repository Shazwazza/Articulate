using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Articulate.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core.Extensions;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace Articulate.Controllers
{

    [PluginController("Articulate")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class ThemeEditorController : BackOfficeNotificationsController
    {
        private readonly IHostEnvironment _hostingEnvironment;

        public ThemeEditorController(
            IHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        public ActionResult<Theme> PostCopyTheme(PostCopyThemeModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            DirectoryInfo[] themeFolderDirectories = GetThemeDirectories(out var themeDirectory);

            DirectoryInfo sourceTheme = themeFolderDirectories.FirstOrDefault(x => x.Name.InvariantEquals(model.ThemeName));
            if (sourceTheme == null)
            {
                return NotFound();
            }

            var articulateUserThemesDirectory = _hostingEnvironment.MapPathContentRoot(PathHelper.UserVirtualThemePath);
            if (!Directory.Exists(articulateUserThemesDirectory))
            {
                Directory.CreateDirectory(articulateUserThemesDirectory);
            }

            DirectoryInfo[] articulateUserThemesDirectories = new DirectoryInfo(articulateUserThemesDirectory).GetDirectories();

            DirectoryInfo destTheme = articulateUserThemesDirectories.FirstOrDefault(x => x.Name.InvariantEquals(model.NewThemeName));

            if (destTheme != null)
            {
                ModelState.AddModelError("value", "The theme " + model.NewThemeName + " already exists");
                return ValidationProblem(ModelState);
            }

            CopyDirectory(sourceTheme, new DirectoryInfo(Path.Combine(articulateUserThemesDirectory, model.NewThemeName)));

            return new Theme()
            {
                Name = model.NewThemeName,
                Path = "-1," + model.NewThemeName
            };
        }

        public IEnumerable<Theme> GetThemes()
        {
            DirectoryInfo[] themeFolderDirectories = GetThemeDirectories(out _);

            IEnumerable<Theme> themes = themeFolderDirectories
                .Select(x => new Theme
                {
                    Name = x.Name
                });

            return themes;
        }

        private DirectoryInfo[] GetThemeDirectories(out string themeFolder)
        {
            themeFolder = _hostingEnvironment.MapPathContentRoot(PathHelper.VirtualThemePath);
            DirectoryInfo[] themeFolderDirectories = new DirectoryInfo(Path.Combine(themeFolder)).GetDirectories();
            return themeFolderDirectories;
        }

        private static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
        {
            if (destination.Exists)
            {
                throw new InvalidOperationException("Theme already exists");
            }

            destination.Create();

            // Copy all files.
            FileInfo[] files = source.GetFiles();
            foreach (FileInfo file in files)
            {
                file.CopyTo(Path.Combine(destination.FullName, file.Name));
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
    }
}
