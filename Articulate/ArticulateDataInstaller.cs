using System;
using System.Linq;
using System.Net.Mime;
using System.Web.UI;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Publishing;

namespace Articulate
{
    public class ArticulateDataInstaller
    {
        public IContent Execute()
        {
            //Create the root node - this will automatically create the authors and archive nodes
            LogHelper.Info<ArticulateDataInstaller>("Creating Articulate root node");
            var root = ApplicationContext.Current.Services.ContentService.CreateContent(
                "Blog", -1, "Articulate");
            root.SetValue("theme", "Shazwazza");
            root.SetValue("blogTitle", "Articulate Blog");
            root.SetValue("blogDescription", "Welcome to my blog");
            root.SetValue("blogLogo", @"{'focalPoint': {'left': 0.51648351648351654,'top': 0.43333333333333335},'src': '/media/1007/capture3.png','crops': []}");            
            root.SetValue("blogBanner", @"{'focalPoint': {'left': 0.35,'top': 0.29588014981273408},'src': '/media/1011/7406981406_1aff1cb527_o.jpg','crops': []}");
            
            ApplicationContext.Current.Services.ContentService.SaveAndPublishWithStatus(root);

            //get the authors and archive nodes and publish them
            LogHelper.Info<ArticulateDataInstaller>("Publishing authors and archive nodes");
            var children = root.Children().ToArray();
            var authors = children.Single(x => x.ContentType.Alias.InvariantEquals("ArticulateAuthors"));
            var archive = children.Single(x => x.ContentType.Alias.InvariantEquals("ArticulateArchive"));
            ApplicationContext.Current.Services.ContentService.SaveAndPublishWithStatus(authors);
            ApplicationContext.Current.Services.ContentService.SaveAndPublishWithStatus(archive);

            //Create the author
            LogHelper.Info<ArticulateDataInstaller>("Creating demo author");
            var author = ApplicationContext.Current.Services.ContentService.CreateContent(
                "Demo author", authors, "ArticulateAuthor");
            author.SetValue("authorBio", "A test Author bio");
            author.SetValue("authorUrl", "http://google.com");
            author.SetValue("authorImage", @"{'focalPoint': {'left': 0.5,'top': 0.5},'src': '/media/1008/random-mask.jpg','crops': []}");
            ApplicationContext.Current.Services.ContentService.SaveAndPublishWithStatus(author);

            //Create a test post
            LogHelper.Info<ArticulateDataInstaller>("Creating test blog post");
            var post = ApplicationContext.Current.Services.ContentService.CreateContent(
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
            ApplicationContext.Current.Services.ContentService.SaveAndPublishWithStatus(post);

            return root;
        }

    }
}