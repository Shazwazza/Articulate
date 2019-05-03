using Articulate.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Packaging;
using Umbraco.Core.Packaging;
using Umbraco.Core.Services;

namespace Articulate.Packaging
{
    public class ArticulateDataInstaller
    {
        private readonly IContentTypeService _contentTypeService;
        private readonly IContentService _contentService;
        private readonly IPackageInstallation _packageInstallation;
        private readonly IPackagingService _packagingService;
        private readonly IDataTypeService _dataTypeService;
        private readonly ILogger _logger;
        private readonly IMediaFileSystem _mediaFileSystem;

        public ArticulateDataInstaller(IContentTypeService contentTypeService, IContentService contentService,
            IPackageInstallation packageInstallation,
            IPackagingService packagingService, IDataTypeService dataTypeService, ILogger logger,
            IMediaFileSystem mediaFileSystem)
        {
            _contentTypeService = contentTypeService;
            _contentService = contentService;
            _packageInstallation = packageInstallation;
            _packagingService = packagingService;
            _dataTypeService = dataTypeService;
            _logger = logger;
            _mediaFileSystem = mediaFileSystem;
        }

        /// <summary>
        /// Installs the Articulate package including schema and content via the package manifest and adds the package to the local repo
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>Returns false if the same version is already installed, in which case nothing is altered.</returns>
        public bool InstallSchemaAndContent(int userId)
        {
            //this will install the package (if it's not already installed), which will in turn run the package action to install the content
            return InstallPackage(userId);
        }

        /// <summary>
        /// Called by the package action to install content
        /// </summary>
        /// <returns></returns>
        public IContent InstallContent()
        {
            //TODO: Need to put the 'ugprader' back since package installation is not going to merge json values such as 
            // the number of crops required. 

            var articulateContentType = _contentTypeService.Get("Articulate");

            if (articulateContentType == null)
            {
                //this should not happen!
                throw new InvalidOperationException("Could not find the Articulate content type");
            }

            //Upgrade();

            var root = _contentService.GetPagedOfType(articulateContentType.Id, 0, int.MaxValue, out long totalRoots, null).FirstOrDefault();
            if (root == null)
            {
                return InstallContentData();
            }

            return null;
        }

        /// <summary>
        /// This will install the Articulate package based on the actual articulate package manifest file which is embedded
        /// into this assembly.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        /// <remarks>
        /// This would be like installing the package in the back office to install all schema, etc... but we do this 
        /// without the full package file, just to install the schema and content and to add the package to the package repo.
        /// </remarks>
        private bool InstallPackage(int userId)
        {
            //TODO: We need to reflect here because this isn't public and we're resolving the internal type from DI
            var parserType = _contentService.GetType().Assembly.GetType("Umbraco.Core.Packaging.CompiledPackageXmlParser");
            if (parserType == null)
                throw new InvalidOperationException("Could not get type Umbraco.Core.Packaging.CompiledPackageXmlParser");
            var parser = Current.Factory.GetInstance(parserType);

            //these are the parameters required, the fake FileInfo doesn't really do anything in this context
            var fakePackageFile = new FileInfo(Path.Combine(IOHelper.MapPath("~/App_Data/TEMP/Articulate"), Guid.NewGuid().ToString(), "fake-package.zip"));
            var xdoc = XDocument.Parse(ArticulateResources.packageManifest); //read in the xdocument package xml
            var appRoot = GetRootDirectorySafe(); //the root folder (based on what is passed in the Core)

            //reflect, call ToCompiledPackage to get the CompiledPackage reference
            CompiledPackage compiledPackage = (CompiledPackage)parser.CallMethod("ToCompiledPackage", xdoc, fakePackageFile, appRoot);

            //TODO: Need to reflect again to get the package definition
            PackageDefinition packageDefinition = (PackageDefinition)typeof(PackageDefinition).CallStaticMethod("FromCompiledPackage", compiledPackage);

            //if it's not installed or it's not the same version, then we need to run the installer
            if (!IsPackageVersionAlreadyInstalled(packageDefinition.Name, packageDefinition.Version, out var sameVersion, out var packageId) || !sameVersion)
            {
                //clear out the files, we don't want to save this package manifest with files since we don't want to delete them on package 
                //uninstallation done in the back office since this will generally only be the case when we are installing via Nuget
                packageDefinition.Files = new List<string>();
                var summary = _packageInstallation.InstallPackageData(packageDefinition, compiledPackage, userId);
                //persist this to the package repo, it will now show up as installed packages in the back office
                _packagingService.SaveInstalledPackage(packageDefinition);
                return true;
            }

            return false;
        }

        // borrowed from Core (it's internal on IOHelper)
        private static string _rootDir = "";
        private static string GetRootDirectorySafe()
        {
            if (String.IsNullOrEmpty(_rootDir) == false)
            {
                return _rootDir;
            }

            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new Uri(codeBase);
            var path = uri.LocalPath;
            var baseDirectory = Path.GetDirectoryName(path);
            if (String.IsNullOrEmpty(baseDirectory))
                throw new Exception("No root directory could be resolved. Please ensure that your Umbraco solution is correctly configured.");

            _rootDir = baseDirectory.Contains("bin")
                           ? baseDirectory.Substring(0, baseDirectory.LastIndexOf("bin", StringComparison.OrdinalIgnoreCase) - 1)
                           : baseDirectory;

            return _rootDir;
        }

        private bool IsPackageVersionAlreadyInstalled(string name, string version, out bool sameVersion, out int packageId)
        {
            var allInstalled = _packagingService.GetAllInstalledPackages();
            var found = allInstalled.Where(x => x.Name == name).ToArray();
            sameVersion = false;

            if (found.Length > 0)
            {
                var foundVersion = found.FirstOrDefault(x =>
                {
                    //match the exact version
                    if (x.Version == version)
                    {
                        return true;
                    }
                    //now try to compare the versions
                    if (Version.TryParse(x.Version, out Version installed) && Version.TryParse(version, out Version selected))
                    {
                        if (installed >= selected)
                            return true;
                    }
                    return false;
                });

                sameVersion = foundVersion != null;

                //this package is already installed, find the highest package id for this package name that is installed
                packageId = found.Max(x => x.Id);
                return true;
            }

            packageId = -1;
            return false;
        }

        //private void Upgrade()
        //{
        //    //For v2.0 we need to manually add some pre-values to the Articulate Cropper,
        //    // https://github.com/Shazwazza/Articulate/issues/80
        //    // https://github.com/Shazwazza/Articulate/issues/135
        //    // The normal upgrade process will upgrade all of the other things apart from the addition of the pre-values
        //    //For v3.0 we need to manually add some pre-values to the Articulate Cropper,
        //    // https://github.com/Shazwazza/Articulate/issues/202

        //    var cropperDt = _dataTypeService.GetDataType("Articulate Cropper");
        //    if (cropperDt != null)
        //    {
        //        if (cropperDt.EditorAlias.InvariantEquals("Umbraco.ImageCropper"))
        //        {
        //            var preVals = cropperDt.ConfigurationAs<ImageCropperConfiguration>();
        //            if (preVals != null)
        //            {
        //                var crops = new ImageCropperConfiguration.Crop[]
        //                {
        //                    new ImageCropperConfiguration.Crop { Alias = "square", Width = 480, Height = 480 },
        //                    new ImageCropperConfiguration.Crop { Alias = "thumbnail", Width = 50, Height = 50 },
        //                    new ImageCropperConfiguration.Crop { Alias = "wide", Width = 1024, Height = 512 },
        //                    new ImageCropperConfiguration.Crop { Alias = "blogPost", Width = 200, Height = 200 }
        //                };

        //                if (preVals.Crops == null || preVals.Crops.Length == 0)
        //                {
        //                    preVals.Crops = crops;
        //                    _dataTypeService.Save(cropperDt);
        //                }
        //                else
        //                {
        //                    //we should merge them since the developer may have added their own
        //                    var currentCrops = preVals.Crops;
        //                    var required = crops.ToDictionary(crop => crop.Alias, crop => false);

        //                    foreach (var cropAlias in currentCrops.Select(x => x.Alias))
        //                    {
        //                        if (required.ContainsKey(cropAlias))
        //                            required[cropAlias] = true;
        //                    }

        //                    //fill in the missing
        //                    foreach (var req in required)
        //                    {
        //                        if (!req.Value)
        //                            preVals.Crops.Append(crops.First(x => x.Alias == req.Key));
        //                    }

        //                    preVals.Crops = crops;
        //                    _dataTypeService.Save(cropperDt);
        //                }                        
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// Ensure the media is saved with the media file system
        /// </summary>
        /// <remarks>
        /// Copy from the 'original' location to the 'default'
        /// </remarks>
        private void InstallMedia()
        {
            _mediaFileSystem.AddFile("articulate/default/logo.png", IOHelper.MapPath("~/media/articulate/original/logo.png"), true, true);
            _mediaFileSystem.AddFile("articulate/default/author.jpg", IOHelper.MapPath("~/media/articulate/original/author.jpg"), true, true);
            _mediaFileSystem.AddFile("articulate/default/banner.jpg", IOHelper.MapPath("~/media/articulate/original/banner.jpg"), true, true);
            _mediaFileSystem.AddFile("articulate/default/post1.jpg", IOHelper.MapPath("~/media/articulate/original/post1.jpg"), true, true);
            _mediaFileSystem.AddFile("articulate/default/post2.jpg", IOHelper.MapPath("~/media/articulate/original/post2.jpg"), true, true);
        }

        private IContent InstallContentData()
        {
            InstallMedia();

            //Create the root node - this will automatically create the authors and archive nodes
            _logger.Info<ArticulateDataInstaller>("Creating Articulate root node");
            var root = _contentService.Create("Blog", Constants.System.Root, "Articulate");
            root.SetValue("theme", "VAPOR");
            root.SetValue("pageSize", 10);
            root.SetValue("categoriesUrlName", "categories");
            root.SetValue("tagsUrlName", "tags");
            root.SetValue("searchUrlName", "search");
            root.SetValue("categoriesPageName", "Categories");
            root.SetValue("tagsPageName", "Tags");
            root.SetValue("searchPageName", "Search results");
            root.SetValue("blogTitle", "Articulate Blog");
            root.SetValue("blogDescription", "Welcome to my blog");
            root.SetValue("extractFirstImage", true);
            root.SetValue("redirectArchive", true);
            root.SetValue("blogLogo", @"{'focalPoint': {'left': 0.5,'top': 0.5},'src': '/media/articulate/default/logo.png','crops': []}");
            root.SetValue("blogBanner", @"{'focalPoint': {'left': 0.5,'top': 0.5},'src': '/media/articulate/default/banner.jpg','crops': []}");

            var result = _contentService.SaveAndPublish(root);
            if (!result.Success)
            {
                _logger.Warn<ArticulateDataInstaller>("Could not create Articulate root node, saving failed with status {Status}, invalid properties are {InvalidProperties} ", result.Result, string.Join(", ", result.InvalidProperties.Select(x => x.Alias)));
                return null;
            }

            //get the authors and archive nodes and publish them
            _logger.Info<ArticulateDataInstaller>("Publishing authors and archive nodes");
            var children = _contentService.GetPagedChildren(root.Id, 0, 10, out var total).ToList();
            var archive = children.FirstOrDefault(x => x.ContentType.Alias == "ArticulateArchive");
            if (archive == null)
            {
                _logger.Warn<ArticulateDataInstaller>("Articulate archive node was not created, cannot proceed to publish");
                return null;
            }
            result = _contentService.SaveAndPublish(archive);
            if (!result.Success)
            {
                _logger.Warn<ArticulateDataInstaller>("Could not create Articulate archive node, saving failed with status {Status}, invalid properties are {InvalidProperties} ", result.Result, string.Join(", ", result.InvalidProperties.Select(x => x.Alias)));
                return null;
            }

            var authors = children.FirstOrDefault(x => x.ContentType.Alias == "ArticulateAuthors");
            if (authors == null)
            {
                _logger.Warn<ArticulateDataInstaller>("Articulate authors node was not created, cannot proceed to publish");
                return null;
            }
            result = _contentService.SaveAndPublish(authors);
            if (!result.Success)
            {
                _logger.Warn<ArticulateDataInstaller>("Could not create Articulate authors node, saving failed with status {Status}, invalid properties are {InvalidProperties} ", result.Result, string.Join(", ", result.InvalidProperties.Select(x => x.Alias)));
                return null;
            }

            //Create the author
            _logger.Info<ArticulateDataInstaller>("Creating demo author");
            var author = _contentService.Create("Jane Doe", authors.Id, "ArticulateAuthor");
            author.SetValue("authorBio", "Jane Doe writes articles for businesses who love coffee as much as she does. Her articles have appeared in a number of coffee related magazines such as beanscenemag.com.au and dailycoffeenews.com. Her articles focus on the health benefits coffee has to offer –but never at the expense of providing an entertaining read.");
            author.SetValue("authorUrl", "https://github.com/shazwazza/articulate");
            author.SetValue("authorImage", @"{'focalPoint': {'left': 0.5,'top': 0.5},'src': '/media/articulate/default/author.jpg','crops': []}");
            result = _contentService.SaveAndPublish(author);
            if (!result.Success)
            {
                _logger.Warn<ArticulateDataInstaller>("Could not create Articulate author node, saving failed with status {Status}, invalid properties are {InvalidProperties} ", result.Result, string.Join(", ", result.InvalidProperties.Select(x => x.Alias)));
                return null;
            }


            //Create a test posts
            _logger.Info<ArticulateDataInstaller>("Creating test blog post 1");

            var post1 = _contentService.Create("Welcome", archive.Id, "ArticulateMarkdown");
            post1.SetValue("author", "Jane Doe");
            post1.SetValue("excerpt", "Hi! Welcome to blogging with Articulate :) This is a fully functional blog engine supporting many features.");
            post1.AssignTags("categories", new[] { "Articulate" });
            post1.AssignTags("tags", new[] { "Cafe", "Markdown" });
            post1.SetValue("publishedDate", DateTime.Now);
            post1.SetValue("postImage", @"{'focalPoint': {'left': 0.5,'top': 0.5},'src': '/media/articulate/default/post1.jpg','crops': []}");
            post1.SetValue("socialDescription", "Welcome to blogging with Articulate, a fully functional blog engine supporting all of the blogging features you'd want.");
            post1.SetValue("markdown", @"## Hi! Welcome to Articulate :)

This is a fully functional blog engine supporting many features:

* Categories & Tags
* Themes
* Live Writer support
* Markdown support
* Easily post from your mobile phone including adding images and photos direct from you camera
* Disqus or Facebook comment support (or build your own)
* Search
* Blogml import/export
* RSS
* Customizable urls
* Author profiles

Articulate is open source and hosted on GitHub:
[https://github.com/Shandem/Articulate/](https://github.com/Shandem/Articulate)

It comes with a few themes which have different features enabled. You can easily change themes on the root Articulate node on the style tab. Themes are super easy to create and in fact the 4 themes shipped with Articulate are MIT licensed themes originally built for the Ghost blogging platform.

Comments are powered by Disqus (Facebook or custom comment engines can be used which can be enabled in the templates).

Live Writer integration is fully functional, to configure Live Writer just use the URL of the Articulate root node and use your Umbraco username/password.

You can post directly from your mobile (including images and photos). This editor can be found at the path of ""/a-new"". Click [Here](../a-new) to see it in action. Now you can post your thoughts wherever you are, from a cafe, on the road, etc... all without needing your computer. 

Enjoy!");
            result = _contentService.SaveAndPublish(post1);
            if (!result.Success)
            {
                _logger.Warn<ArticulateDataInstaller>("Could not create Articulate post node, saving failed with status {Status}, invalid properties are {InvalidProperties} ", result.Result, string.Join(", ", result.InvalidProperties.Select(x => x.Alias)));
                return null;
            }

            var post2 = _contentService.Create("Latte art", archive.Id, "ArticulateMarkdown");
            post2.SetValue("author", "Jane Doe");
            post2.SetValue("excerpt", "Latte art is a method of preparing coffee created by pouring steamed milk into a shot of espresso, resulting in a pattern or design on the surface of the latte. ");
            post2.AssignTags("categories", new[] { "Coffee" });
            post2.AssignTags("tags", new[] { "Cafe", "Milk", "Espresso" });
            post2.SetValue("publishedDate", DateTime.Now.AddDays(-10));
            post2.SetValue("postImage", @"{'focalPoint': {'left': 0.5,'top': 0.5},'src': '/media/articulate/default/post2.jpg','crops': []}");
            post2.SetValue("socialDescription", "Latte art is a method of preparing coffee created by pouring steamed milk into a shot of espresso, resulting in a pattern or design on the surface of the latte. ");
            post2.SetValue("markdown", @"Latte art is a method of preparing coffee created by pouring steamed milk into a shot of espresso, resulting in a pattern or design on the surface of the latte. Latte art is hard to create consistently because of the many factors involved with creating coffee, from the coffee acidity, temperature, to the type of milk and equipment being used. Don't expect that you'll be able to make latte art the first time you try, in fact it will probably take you a great number of tries to make something work and you'll be hoping that you're using some quality equipment, otherwise you'll stand no chance.

Good latte art means you've found a cafe with a good barista so there's a good chance if you're seeing a a great design in your coffee, it's also going to taste wonderful.");
            result = _contentService.SaveAndPublish(post2);
            if (!result.Success)
            {
                _logger.Warn<ArticulateDataInstaller>("Could not create Articulate post node, saving failed with status {Status}, invalid properties are {InvalidProperties} ", result.Result, string.Join(", ", result.InvalidProperties.Select(x => x.Alias)));
                return null;
            }

            return root;
        }
    }
}
