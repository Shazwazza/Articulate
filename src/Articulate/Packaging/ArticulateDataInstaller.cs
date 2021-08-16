using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Articulate.Packaging
{
    // TODO: I don't think we need any of this!

    public class ArticulateDataInstaller
    {
        private readonly IContentTypeService _contentTypeService;
        private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
        private readonly IContentService _contentService;
        private readonly IDataTypeService _dataTypeService;
        private readonly ILocalizationService _languageService;
        private readonly ILogger<ArticulateDataInstaller> _logger;
        private readonly ILocalizationService _localizationService;
        private readonly PropertyEditorCollection _dataEditors;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly MediaFileManager _mediaFileManager;

        public ArticulateDataInstaller(
            IContentTypeService contentTypeService,
            IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
            IContentService contentService,
            IDataTypeService dataTypeService,
            ILocalizationService languageService,
            ILogger<ArticulateDataInstaller> logger,
            ILocalizationService localizationService,
            PropertyEditorCollection dataEditors,
            IJsonSerializer jsonSerializer,
            MediaFileManager mediaFileManager)
        {
            _contentTypeService = contentTypeService;
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
            _contentService = contentService;
            _dataTypeService = dataTypeService;
            _languageService = languageService;
            _logger = logger;
            _localizationService = localizationService;
            _dataEditors = dataEditors;
            _jsonSerializer = jsonSerializer;
            _mediaFileManager = mediaFileManager;
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

        private IContent CreateRoot()
        {
            var articulateType = _contentTypeService.Get("Articulate");
            if (articulateType == null)
            {
                throw new InvalidOperationException("No Articulate document type");
            }

            //Create the root node - this will automatically create the authors and archive nodes
            _logger.LogInformation("Creating Articulate root node");
            var root = _contentService.CreateWithInvariantOrDefaultCultureName("Blog", Constants.System.Root, articulateType, _localizationService);

            root.SetInvariantOrDefaultCultureValue("theme", "VAPOR", articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("pageSize", 10, articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("categoriesUrlName", "categories", articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("tagsUrlName", "tags", articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("searchUrlName", "search", articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("categoriesPageName", "Categories", articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("tagsPageName", "Tags", articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("searchPageName", "Search results", articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("blogTitle", "Articulate Blog", articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("blogDescription", "Welcome to my blog", articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("extractFirstImage", true, articulateType, _localizationService);
            root.SetInvariantOrDefaultCultureValue("redirectArchive", true, articulateType, _localizationService);

            // Deal with files...
            SetImage(root, articulateType, "logo.png", "blogLogo");
            SetImage(root, articulateType, "banner.jpg", "blogBanner");

            var result = _contentService.SaveAndPublish(root);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Could not create Articulate root node, saving failed with status {result.Result}, invalid properties are {string.Join(", ", result.InvalidProperties.Select(x => x.Alias))}");
            }

            return result.Content;
        }

        private IContent CreateSubNode(IContent root, string contentTypeAlias, string nodeName, string defaultLang)
        {
            IContentType authorContentType = _contentTypeService.Get(contentTypeAlias);
            if (authorContentType == null)
            {
                throw new InvalidOperationException($"No {contentTypeAlias} doc type");
            }
            if (authorContentType.VariesByCulture())
            {
                IContent authors = _contentService.Create("", root, contentTypeAlias);
                authors.SetCultureName(nodeName, defaultLang);
                _contentService.SaveAndPublish(authors);
                return authors;
            }
            else
            {
                var authors = _contentService.CreateAndSave(nodeName, root, contentTypeAlias);
                _contentService.SaveAndPublish(authors);
                return authors;
            }
        }

        private void CreateAuthor(IContent authors)
        {
            var authorType = _contentTypeService.Get("ArticulateAuthor");
            if (authorType == null)
            {
                throw new InvalidOperationException("No 'ArticulateAuthor' content type found");
            }

            //Create the author
            _logger.LogInformation("Creating demo author");
            var author = _contentService.CreateWithInvariantOrDefaultCultureName("Jane Doe", authors.Id, authorType, _localizationService);

            author.SetInvariantOrDefaultCultureValue("authorBio", "Jane Doe writes articles for businesses who love coffee as much as she does. Her articles have appeared in a number of coffee related magazines such as beanscenemag.com.au and dailycoffeenews.com. Her articles focus on the health benefits coffee has to offer –but never at the expense of providing an entertaining read.", authorType, _localizationService);
            author.SetInvariantOrDefaultCultureValue("authorUrl", "https://github.com/shazwazza/articulate", authorType, _localizationService);

            // Deal with files...
            SetImage(author, authorType, "author.jpg", "authorImage");

            var result = _contentService.SaveAndPublish(author);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Could not create Articulate author node, saving failed with status {result.Result}, invalid properties are {string.Join(", ", result.InvalidProperties.Select(x => x.Alias))} ");
            }
        }

        private IContent InstallContentData()
        {
            var defaultLang = _languageService.GetDefaultLanguageIsoCode();

            var root = CreateRoot();
            var archive = CreateSubNode(root, ArticulateConstants.ArticulateArchiveContentTypeAlias, ArticulateConstants.ArticlesDefaultName, defaultLang);
            var authors = CreateSubNode(root, ArticulateConstants.ArticulateAuthorsContentTypeAlias, ArticulateConstants.AuthorsDefaultName, defaultLang);

            CreateAuthor(authors);

            //Create a test posts
            _logger.LogInformation("Creating test blog post 1");

            var markdownType = _contentTypeService.Get("ArticulateMarkdown");
            if (markdownType == null)
            {
                _logger.LogWarning("No 'ArticulateMarkdown' content type found");
                return null;
            }
            var post1 = _contentService.CreateWithInvariantOrDefaultCultureName("Welcome", archive.Id, markdownType, _localizationService);

            post1.SetInvariantOrDefaultCultureValue("author", "Jane Doe", markdownType, _localizationService);
            post1.SetInvariantOrDefaultCultureValue("excerpt", "Hi! Welcome to blogging with Articulate :) This is a fully functional blog engine supporting many features.", markdownType, _localizationService);
            post1.AssignInvariantOrDefaultCultureTags("categories", new[] { "Articulate" }, markdownType, _localizationService, _dataTypeService, _dataEditors, _jsonSerializer);
            post1.AssignInvariantOrDefaultCultureTags("tags", new[] { "Cafe", "Markdown" }, markdownType, _localizationService, _dataTypeService, _dataEditors, _jsonSerializer);
            post1.SetInvariantOrDefaultCultureValue("publishedDate", DateTime.Now, markdownType, _localizationService);            
            post1.SetInvariantOrDefaultCultureValue("socialDescription", "Welcome to blogging with Articulate, a fully functional blog engine supporting all of the blogging features you'd want.", markdownType, _localizationService);
            post1.SetInvariantOrDefaultCultureValue("markdown", @"## Hi! Welcome to Articulate :)

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

Enjoy!", markdownType, _localizationService);

            // Deal with files...
            SetImage(post1, markdownType, "post1.jpg", "postImage");

            var result = _contentService.SaveAndPublish(post1);
            if (!result.Success)
            {
                _logger.LogWarning("Could not create Articulate post node, saving failed with status {Status}, invalid properties are {InvalidProperties} ", result.Result, string.Join(", ", result.InvalidProperties.Select(x => x.Alias)));
                return null;
            }

            var post2 = _contentService.CreateWithInvariantOrDefaultCultureName("Latte art", archive.Id, markdownType, _localizationService);

            post2.SetInvariantOrDefaultCultureValue("author", "Jane Doe", markdownType, _localizationService);
            post2.SetInvariantOrDefaultCultureValue("excerpt", "Latte art is a method of preparing coffee created by pouring steamed milk into a shot of espresso, resulting in a pattern or design on the surface of the latte. ", markdownType, _localizationService);
            post2.AssignInvariantOrDefaultCultureTags("categories", new[] { "Coffee" }, markdownType, _localizationService, _dataTypeService, _dataEditors, _jsonSerializer);
            post2.AssignInvariantOrDefaultCultureTags("tags", new[] { "Cafe", "Milk", "Espresso" }, markdownType, _localizationService, _dataTypeService, _dataEditors, _jsonSerializer);
            post2.SetInvariantOrDefaultCultureValue("publishedDate", DateTime.Now.AddDays(-10), markdownType, _localizationService);            
            post2.SetInvariantOrDefaultCultureValue("socialDescription", "Latte art is a method of preparing coffee created by pouring steamed milk into a shot of espresso, resulting in a pattern or design on the surface of the latte. ", markdownType, _localizationService);
            post2.SetInvariantOrDefaultCultureValue("markdown", @"Latte art is a method of preparing coffee created by pouring steamed milk into a shot of espresso, resulting in a pattern or design on the surface of the latte. Latte art is hard to create consistently because of the many factors involved with creating coffee, from the coffee acidity, temperature, to the type of milk and equipment being used. Don't expect that you'll be able to make latte art the first time you try, in fact it will probably take you a great number of tries to make something work and you'll be hoping that you're using some quality equipment, otherwise you'll stand no chance.

Good latte art means you've found a cafe with a good barista so there's a good chance if you're seeing a a great design in your coffee, it's also going to taste wonderful.", markdownType, _localizationService);

            // Deal with files...
            SetImage(post2, markdownType, "post2.jpg", "postImage");

            result = _contentService.SaveAndPublish(post2);
            if (!result.Success)
            {
                _logger.LogWarning("Could not create Articulate post node, saving failed with status {Status}, invalid properties are {InvalidProperties} ", result.Result, string.Join(", ", result.InvalidProperties.Select(x => x.Alias)));
                return null;
            }

            return root;
        }

        private void SetImage(IContent content, IContentTypeComposition contentTypeComposition, string imageName, string propertyAlias)
        {
            using (var imageStream = GetEmbeddedImage(imageName))
            {
                var imagePath = content.StoreFile(_mediaFileManager, _contentTypeBaseServiceProvider, propertyAlias, imageName, imageStream, null);
                content.SetInvariantOrDefaultCultureValue(propertyAlias, $@"{{'focalPoint': {{'left': 0.5,'top': 0.5}},'src': '{_mediaFileManager.FileSystem.GetUrl(imagePath)}','crops': []}}", contentTypeComposition, _localizationService);
            }
        }

        private Stream GetEmbeddedImage(string imageFile)
        {
            // lookup the embedded resource by convention
            Assembly currentAssembly = GetType().Assembly;
            var fileName = $"{GetType().Namespace}.{imageFile}";
            Stream stream = currentAssembly.GetManifestResourceStream(fileName);
            if (stream == null)
            {
                throw new FileNotFoundException("Cannot find the embedded file.", fileName);
            }
            return stream;
        }
    }
}
