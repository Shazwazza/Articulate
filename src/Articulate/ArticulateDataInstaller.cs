using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Articulate.Resources;
using umbraco.cms.businesslogic.packager;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Composing;

namespace Articulate
{
    public class ArticulateDataInstaller
    {
        private readonly int _userId;

        public ArticulateDataInstaller(int userId)
        {
            _userId = userId;
        }
        
        public IContent Execute(out bool packageInstalled)
        {
            packageInstalled = InstallPackage();

            //TODO: Need to put the 'ugprader' back since package installation is not going to merge json values such as 
            // the number of crops required. 

            var articulateContentType = Current.Services.ContentTypeService.Get("Articulate");

            if (articulateContentType == null)
            {
                //this should not happen!
                throw new InvalidOperationException("Could not find the Articulate content type");
            }

            Upgrade();

            var root = _services.ContentService.GetContentOfContentType(articulateContentType.Id).FirstOrDefault();
            if (root == null)
            {
                return InstallContent();
            }

            return null;
        }

        private bool InstallPackage()
        {
            //check if it's already installed
            //var allInstalled = InstalledPackage.GetAllInstalledPackages();

            //need to save the package manifest to a temp folder since that is how this package installer logic works
            var tempFile = Path.Combine(IOHelper.MapPath("~/App_Data/TEMP/Articulate"), Guid.NewGuid().ToString(), "package.xml");
            var tempDir = Path.GetDirectoryName(tempFile);
            Directory.CreateDirectory(tempDir);

            try
            {
                System.IO.File.WriteAllText(tempFile, ArticulateResources.packageManifest);
                var ins = new global::umbraco.cms.businesslogic.packager.Installer(_userId);
                
                ins.LoadConfig(tempDir);

                int packageId;
                bool sameVersion;
                if (IsPackageVersionAlreadyInstalled(ins.Name, ins.Version, out sameVersion, out packageId))
                {
                    //if it's the same version, we don't need to install anything
                    if (!sameVersion)
                    {
                        var pckId = ins.CreateManifest(tempDir, Guid.NewGuid().ToString(), "65194810-1f85-11dd-bd0b-0800200c9a66");
                        ins.InstallBusinessLogic(pckId, tempDir);
                        return true;
                    }
                    return false;
                }
                else
                {
                    var pckId = ins.CreateManifest(tempDir, Guid.NewGuid().ToString(), "65194810-1f85-11dd-bd0b-0800200c9a66");
                    ins.InstallBusinessLogic(pckId, tempDir);
                    return true;
                }
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                {
                    System.IO.File.Delete(tempFile);
                }
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
            }
        }

        //borrowed from Core
        private bool IsPackageVersionAlreadyInstalled(string name, string version, out bool sameVersion, out int packageId)
        {
            var allInstalled = InstalledPackage.GetAllInstalledPackages();
            var found = allInstalled.Where(x => x.Data.Name == name).ToArray();
            sameVersion = false;

            if (found.Length  > 0)
            {
                var foundVersion = found.FirstOrDefault(x =>
                {
                    //match the exact version
                    if (x.Data.Version == version)
                    {
                        return true;
                    }
                    //now try to compare the versions
                    if (Version.TryParse(x.Data.Version, out Version installed) && Version.TryParse(version, out Version selected))
                    {
                        if (installed >= selected)
                            return true;
                    }
                    return false;
                });

                sameVersion = foundVersion != null;

                //this package is already installed, find the highest package id for this package name that is installed
                packageId = found.Max(x => x.Data.Id);
                return true;
            }

            packageId = -1;
            return false;
        }

        private void Upgrade()
        {
            //For v2.0 we need to manually add some pre-values to the Articulate Cropper,
            // https://github.com/Shazwazza/Articulate/issues/80
            // https://github.com/Shazwazza/Articulate/issues/135
            // The normal upgrade process will upgrade all of the other things apart from the addition of the pre-values
            //For v3.0 we need to manually add some pre-values to the Articulate Cropper,
            // https://github.com/Shazwazza/Articulate/issues/202

            var cropperDt = _services.DataTypeService.GetDataTypeDefinitionByName("Articulate Cropper");
            if (cropperDt != null)
            {
                if (cropperDt.PropertyEditorAlias.InvariantEquals("Umbraco.ImageCropper"))
                {
                    var preVals = _services.DataTypeService.GetPreValuesCollectionByDataTypeId(cropperDt.Id);
                    if (preVals != null)
                    {
                        var crops = new[]
                        {
                            new {alias = "square", width = 480, height = 480},
                            new {alias = "thumbnail", width = 50, height = 50},
                            new {alias = "wide", width = 1024, height = 512}
                        };

                        if (preVals.PreValuesAsDictionary["crops"] == null
                            || preVals.PreValuesAsDictionary["crops"].Value.IsNullOrWhiteSpace())
                        {
                            //there aren't any so we need to add them all                                                        
                            preVals.PreValuesAsDictionary["crops"] = new PreValue(JsonConvert.SerializeObject(crops));

                            _services.DataTypeService.SavePreValues(cropperDt.Id, preVals.PreValuesAsDictionary);
                        }
                        else
                        {
                            //we should merge them since the developer may have added their own
                            var json = JsonConvert.DeserializeObject<JArray>(preVals.PreValuesAsDictionary["crops"].Value);
                            var required = crops.ToDictionary(crop => crop.alias, crop => false);
                            foreach (var prop in json.Children<JObject>().SelectMany(x => x.Property("alias")))
                            {
                                var val = prop.Value<string>();
                                if (required.ContainsKey(prop.Value<string>()))
                                    required[val] = true;
                            }
                            //fill in the missing
                            foreach (var req in required)
                            {
                                if (!req.Value)
                                    json.Add(JObject.FromObject(crops.First(x => x.alias == req.Key)));
                            }

                            preVals.PreValuesAsDictionary["crops"] = new PreValue(JsonConvert.SerializeObject(json));

                            _services.DataTypeService.SavePreValues(cropperDt.Id, preVals.PreValuesAsDictionary);
                        }                        
                    }
                }
            }
        }

        private IContent InstallContent()
        {
            //Create the root node - this will automatically create the authors and archive nodes
            Current.Logger.Info<ArticulateDataInstaller>("Creating Articulate root node");
            var root = Current.Services.ContentService.CreateContent(
                "Blog", Udi.Create(Constants.UdiEntityType.Document, Constants.System.RootString), "Articulate");
            root.SetValue("theme", "VAPOR");
            root.SetValue("blogTitle", "Articulate Blog");
            root.SetValue("blogDescription", "Welcome to my blog");
            root.SetValue("extractFirstImage", true);
            root.SetValue("redirectArchive", true);
            root.SetValue("blogLogo", @"{'focalPoint': {'left': 0.51648351648351654,'top': 0.43333333333333335},'src': '/media/articulate/default/capture3.png','crops': []}");
            root.SetValue("blogBanner", @"{'focalPoint': {'left': 0.35,'top': 0.29588014981273408},'src': '/media/articulate/default/7406981406_1aff1cb527_o.jpg','crops': []}");

            Current.Services.ContentService.SaveAndPublish(root);

            //get the authors and archive nodes and publish them
            Current.Logger.Info<ArticulateDataInstaller>("Publishing authors and archive nodes");
            var children = root.Children().ToArray();
            var authors = children.First(x => x.ContentType.Alias.InvariantEquals("ArticulateAuthors"));
            var archive = children.First(x => x.ContentType.Alias.InvariantEquals("ArticulateArchive"));
            Current.Services.ContentService.SaveAndPublish(authors);
            Current.Services.ContentService.SaveAndPublish(archive);

            //Create the author
            Current.Logger.Info<ArticulateDataInstaller>("Creating demo author");
            var author = Current.Services.ContentService.CreateContent(
                "Demo author", authors, "ArticulateAuthor");
            author.SetValue("authorBio", "A test Author bio");
            author.SetValue("authorUrl", "http://google.com");
            author.SetValue("authorImage", @"{'focalPoint': {'left': 0.5,'top': 0.5},'src': '/media/articulate/default/random-mask.jpg','crops': []}");
            Current.Services.ContentService.SaveAndPublish(author);

            //Create a test post
            Current.Logger.Info<ArticulateDataInstaller>("Creating test blog post");
            var post = Current.Services.ContentService.CreateContent(
                "Test post", archive, "ArticulateMarkdown");
            post.SetValue("author", "Demo author");
            post.SetValue("excerpt", "Hi! Welcome to blogging with Articulate :) This is a fully functional blog engine supporting many features.");
            post.AssignTags("categories", new[] { "TestCategory" }, true, "ArticulateCategories");
            post.AssignTags("tags", new[] { "TestTag" }, true, "ArticulateTags");
            post.SetValue("publishedDate", DateTime.Now);
            post.SetValue("socialDescription", "This article is the bomb!!! Write a description that is more suitable for social sharing than a standard meta description.");
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
            Current.Services.ContentService.SaveAndPublish(post);

            return root;
        }
    }
}