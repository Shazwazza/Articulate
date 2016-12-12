using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Articulate
{
    public class ArticulateDataInstaller
    {
        private readonly ServiceContext _services;

        public ArticulateDataInstaller(ServiceContext services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            _services = services;
        }

        [Obsolete("Use the ctor with all dependencies instead")]
        public ArticulateDataInstaller()
            : this(ApplicationContext.Current.Services)
        {
        }

        public IContent Execute()
        {
            var articulateContentType = _services.ContentTypeService.GetContentType("Articulate");

            if (articulateContentType == null)
            {
                //this should not happen!
                LogHelper.Warn<ArticulateDataInstaller>("Could not find the Articulate content type");
                return null;
            }

            var root = _services.ContentService.GetContentOfContentType(articulateContentType.Id).FirstOrDefault();
            if (root == null)
            {
                return Install();
            }

            Upgrade();
            return root;
        }

        private void Upgrade()
        {
            //For v2.0 we need to manually add some pre-values to the Articulate Cropper,
            // https://github.com/Shazwazza/Articulate/issues/80
            // https://github.com/Shazwazza/Articulate/issues/135
            // The normal upgrade process will upgrade all of the other things apart from the addition of the pre-values

            var cropperDt = _services.DataTypeService.GetDataTypeDefinitionByName("Articulate Cropper");
            if (cropperDt != null)
            {
                if (cropperDt.PropertyEditorAlias.InvariantEquals("Umbraco.ImageCropper"))
                {
                    var preVals = _services.DataTypeService.GetPreValuesCollectionByDataTypeId(cropperDt.Id);
                    if (preVals != null)
                    {
                        if (!HasPrevalues(preVals))
                        {
                            //need to add the crops
                            var crops = new[]
                            {
                                new {alias = "blogPost", width = 200, height=200},
                                new {alias = "thumbnail", width = 50, height=50}
                            };
                            preVals.PreValuesAsDictionary["crops"] = new PreValue(JsonConvert.SerializeObject(crops));

                            _services.DataTypeService.SavePreValues(cropperDt.Id, preVals.PreValuesAsDictionary);
                        }
                    }
                }
            }
        }

        private bool HasPrevalues(PreValueCollection preVals)
        {
            if (preVals.PreValuesAsDictionary["crops"] == null
                || preVals.PreValuesAsDictionary["crops"].Value.IsNullOrWhiteSpace())
            {
                return false;
            }

            try
            {
                var array = JsonConvert.DeserializeObject<JArray>(preVals.PreValuesAsDictionary["crops"].Value);
                return array.Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IContent Install()
        {
            //Create the root node - this will automatically create the authors and archive nodes
            LogHelper.Info<ArticulateDataInstaller>("Creating Articulate root node");
            var root = _services.ContentService.CreateContent(
                "Blog", -1, "Articulate");
            root.SetValue("theme", "Shazwazza");
            root.SetValue("blogTitle", "Articulate Blog");
            root.SetValue("blogDescription", "Welcome to my blog");
            root.SetValue("extractFirstImage", true);
            root.SetValue("blogLogo", @"{'focalPoint': {'left': 0.51648351648351654,'top': 0.43333333333333335},'src': '/media/articulate/default/capture3.png','crops': []}");
            root.SetValue("blogBanner", @"{'focalPoint': {'left': 0.35,'top': 0.29588014981273408},'src': '/media/articulate/default/7406981406_1aff1cb527_o.jpg','crops': []}");

            _services.ContentService.SaveAndPublishWithStatus(root);

            //get the authors and archive nodes and publish them
            LogHelper.Info<ArticulateDataInstaller>("Publishing authors and archive nodes");
            var children = root.Children().ToArray();
            var authors = children.Single(x => x.ContentType.Alias.InvariantEquals("ArticulateAuthors"));
            var archive = children.Single(x => x.ContentType.Alias.InvariantEquals("ArticulateArchive"));
            _services.ContentService.SaveAndPublishWithStatus(authors);
            _services.ContentService.SaveAndPublishWithStatus(archive);

            //Create the author
            LogHelper.Info<ArticulateDataInstaller>("Creating demo author");
            var author = _services.ContentService.CreateContent(
                "Demo author", authors, "ArticulateAuthor");
            author.SetValue("authorBio", "A test Author bio");
            author.SetValue("authorUrl", "http://google.com");
            author.SetValue("authorImage", @"{'focalPoint': {'left': 0.5,'top': 0.5},'src': '/media/articulate/default/random-mask.jpg','crops': []}");
            _services.ContentService.SaveAndPublishWithStatus(author);

            //Create a test post
            LogHelper.Info<ArticulateDataInstaller>("Creating test blog post");
            var post = _services.ContentService.CreateContent(
                "Test post", archive, "ArticulateMarkdown");
            post.SetValue("author", "Demo author");
            post.SetValue("excerpt", "Hi! Welcome to blogging with Articulate :) This is a fully functional blog engine supporting many features.");
            post.SetTags("categories", new[] { "TestCategory" }, true, "ArticulateCategories");
            post.SetTags("tags", new[] { "TestTag" }, true, "ArticulateTags");
            post.SetValue("publishedDate", DateTime.Now);
            post.SetValue("markdown", @"Hi! Welcome to Articulate :)

This is a fully functional blog engine supporting many features:

* Categories & Tags
* Themes
* Live Writer support
* Markdown support
* Easily post from your mobile phone including adding images and photos direct from you camera
* Disqus, Google+ or Facebook comment support
* Search
* Blogml import/export
* RSS
* Customizable urls
* Author profiles

Articulate is open source and hosted on GitHub:
[https://github.com/Shandem/Articulate/](https://github.com/Shandem/Articulate)

It comes with 4 themes which have different features enabled. You can easily change themes on the root Articulate node on the style tab. Themes are super easy to create and in fact the 4 themes shipped with Articulate are MIT licensed themes originally built for the Ghost blogging platform.

Comments are powered by Disqus (Google+ or Facebook are also supported and can be enabled in the templates).

Live Writer integration is fully functional, to configure Live Writer just use the URL of the Articulate root node and use your Umbraco username/password.

You can post directly from your mobile (including images and photos). This editor can be found at the path of ""/a-new"". As an example if Articulate is your root node in Umbraco, then the URL would be:

http://yoursiteurl.com/a-new

Enjoy!");
            _services.ContentService.SaveAndPublishWithStatus(post);

            return root;
        }
    }
}