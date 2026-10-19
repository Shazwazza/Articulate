#nullable enable
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Argotic.Syndication.Specialized;
using Articulate.ImportExport;
using Articulate.Options;
using Articulate.Services;
using Articulate.Syndication.BlogML;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Articulate.Tests.ImportExport
{
    [TestFixture]
    public class BlogMlArchiveTests
    {
        private static readonly Guid FirstKey = Guid.NewGuid();
        private static readonly Guid SecondKey = Guid.NewGuid();

        [TestCase("Other", null, "hello")]
        [TestCase("Hello", null, "hello (1)")]
        [TestCase("Hello", " Hello-1 ", "hello (2)")]
        [TestCase(" ", null, "hello")]
        [TestCase("Hello", " Hello-1 ", "hello (2)", "en-US")]
        public void EnsureUniqueSlug_suffixes_like_umbraco(string siblingName, string? siblingUrlName, string expected, string? culture = null)
        {
            var helper = new DefaultShortStringHelper(new DefaultShortStringHelperConfig());
            IContentBase[] siblings =
            [
                Mock.Of<IContentBase>(x => x.Name == siblingName),
                Mock.Of<IContentBase>(x => x.Name == "Unrelated"
                    && x.GetValue<string>(Umbraco.Cms.Core.Constants.Conventions.Content.UrlName, culture, null, false) == siblingUrlName),
            ];

            Assert.That(BlogMlImporter.EnsureUniqueSlug("hello", BlogMlImporter.CountSlugs(siblings, helper, culture), helper), Is.EqualTo(expected));
        }

        [Test]
        public void Extension_writes_element_only_when_key_or_name_is_set()
        {
            Assert.That(Write(new ArchiveSyndicationExtension()), Is.Empty);
            Assert.That(XElement.Parse(Write(new ArchiveSyndicationExtension { ArchiveName = "Blog" })).Attribute(ArchiveSyndicationExtension.KeyAttribute), Is.Null);

            var element = XElement.Parse(Write(new ArchiveSyndicationExtension
            {
                ArchiveKey = FirstKey,
                ArchiveName = "Blog"
            }));

            Assert.That(element.Name, Is.EqualTo(XName.Get(ArchiveSyndicationExtension.ElementName, ArchiveSyndicationExtension.Namespace)));
            Assert.That(element.Attribute(ArchiveSyndicationExtension.KeyAttribute)?.Value, Is.EqualTo(FirstKey.ToString("D")));
            Assert.That(element.Attribute(ArchiveSyndicationExtension.NameAttribute)?.Value, Is.EqualTo("Blog"));
        }

        [Test]
        public void Extension_loads_key_and_name()
        {
            var extension = new ArchiveSyndicationExtension();
            var element = XElement.Parse($"<post xmlns=\"http://blogml.org/\"><archive xmlns=\"{ArchiveSyndicationExtension.Namespace}\" key=\"{FirstKey:D}\" name=\"Blog\" /></post>");

            Assert.That(extension.Load(element.CreateNavigator()), Is.True);
            Assert.That(extension.ArchiveKey, Is.EqualTo(FirstKey));
            Assert.That(extension.ArchiveName, Is.EqualTo("Blog"));
        }

        [Test]
        public void Archive_attribute_parsed_by_key_name_and_missing()
        {
            XDocument marked = Document($"<archive xmlns=\"{ArchiveSyndicationExtension.Namespace}\" key=\"{FirstKey:D}\" name=\"Blog\" />");

            ArchiveSyndicationExtension? archive = Marker(marked);
            Assert.That(archive?.ArchiveKey, Is.EqualTo(FirstKey));
            Assert.That(archive?.ArchiveName, Is.EqualTo("Blog"));
            Assert.That(Marker(Document(string.Empty)), Is.Null);
        }

        [Test]
        public void Lookup_keeps_first_archive_for_duplicate_post_id()
        {
            var xDoc = XDocument.Parse(
                $"<blog xmlns=\"http://www.blogml.com/2006/09/BlogML\"><posts>" +
                $"<post id=\"post-1\"><archive xmlns=\"{ArchiveSyndicationExtension.Namespace}\" name=\"First\" /></post>" +
                $"<post id=\"post-1\"><archive xmlns=\"{ArchiveSyndicationExtension.Namespace}\" name=\"Second\" /></post>" +
                "</posts></blog>");

            Assert.That(Marker(xDoc)?.ArchiveName, Is.EqualTo("First"));
        }

        [Test]
        public void Post_lookup_keeps_first_duplicate_id_and_falls_back_to_name()
        {
            (Dictionary<string, XElement> ById, Dictionary<string, XElement> ByName) lookup = BlogMlImporter.BuildPostElementLookup(XDocument.Parse(
                "<blog xmlns=\"http://www.blogml.com/2006/09/BlogML\"><posts>" +
                "<post id=\"p1\"><post-name>First</post-name></post>" +
                "<post id=\"p1\"><post-name>Second</post-name></post>" +
                "<post id=\"p2\"><post-name>Named</post-name></post>" +
                "</posts></blog>"));

            Assert.That(BlogMlImporter.FindPostElement(lookup, new BlogMLPost { Id = "p1" }), Is.SameAs(lookup.ByName["First"]));
            Assert.That(
                BlogMlImporter.FindPostElement(lookup, new BlogMLPost { Id = "missing", Name = new BlogMLTextConstruct("Named") }),
                Is.SameAs(lookup.ById["p2"]));
            Assert.That(BlogMlImporter.FindPostElement(lookup, new BlogMLPost { Id = "missing", Name = new BlogMLTextConstruct("None") }), Is.Null);
        }

        [Test]
        public void ToBlogMlDocument_parses_posts_authors_and_categories()
        {
            BlogMLDocument document = BlogMlImporter.ToBlogMlDocument(XDocument.Parse(
                "<blog xmlns=\"http://www.blogml.com/2006/09/BlogML\" date-created=\"2020-01-01T00:00:00\"><title type=\"text\">Blog</title>" +
                "<authors><author id=\"a1\"><title type=\"text\">Ann</title></author></authors>" +
                "<categories><category id=\"c1\"><title type=\"text\">Cat</title></category></categories>" +
                "<posts><post id=\"p1\"><title type=\"text\">T</title><post-name type=\"text\">Hello World</post-name></post></posts>" +
                "</blog>"));

            Assert.That(document.Posts.Single().Name.Content, Is.EqualTo("Hello World"));
            Assert.That(document.Authors.Single().Id, Is.EqualTo("a1"));
            Assert.That(document.Categories.Single().Title.Content, Is.EqualTo("Cat"));
        }

        [Test]
        public void FindExistingPost_matches_import_id_or_exact_slug_only()
        {
            var helper = new DefaultShortStringHelper(new DefaultShortStringHelperConfig());
            IContent byId = Mock.Of<IContent>(x => x.Name == "Other" && x.GetValue<string>("importId", null, null, false) == "p1");
            IContent prefixed = Mock.Of<IContent>(x => x.Name == "hello-world-2");
            IContent exact = Mock.Of<IContent>(x => x.Name == "hello-world");
            var named = new BlogMLPost { Name = new BlogMLTextConstruct("Hello World") };

            var byImportId = new Dictionary<string, IContent> { ["p1"] = byId };
            var bySlug = new Dictionary<string, IContent>(StringComparer.OrdinalIgnoreCase) { ["hello-world-2"] = prefixed, ["Hello World".ToUrlSegment(helper)] = exact };
            var prefixedOnly = new Dictionary<string, IContent>(StringComparer.OrdinalIgnoreCase) { ["hello-world-2"] = prefixed };

            Assert.That(BlogMlImporter.FindExistingPost(byImportId, bySlug, new BlogMLPost { Id = "p1" }, helper), Is.SameAs(byId));
            Assert.That(BlogMlImporter.FindExistingPost(byImportId, bySlug, new BlogMLPost { Id = "unknown", Name = new BlogMLTextConstruct("Hello World") }, helper), Is.Null);
            Assert.That(BlogMlImporter.FindExistingPost(byImportId, bySlug, named, helper), Is.SameAs(exact));
            Assert.That(BlogMlImporter.FindExistingPost(byImportId, prefixedOnly, named, helper), Is.Null);
            Assert.That(BlogMlImporter.FindExistingPost(byImportId, bySlug, new BlogMLPost(), helper), Is.Null);
        }

        [Test]
        public void Slug_counts_keep_segment_taken_until_every_holder_releases()
        {
            var helper = new DefaultShortStringHelper(new DefaultShortStringHelperConfig());
            var taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            BlogMlImporter.TakeSlug(taken, "hello");
            BlogMlImporter.TakeSlug(taken, "hello");

            BlogMlImporter.ReleaseSlug(taken, "hello");
            Assert.That(BlogMlImporter.EnsureUniqueSlug("hello", taken, helper), Is.EqualTo("hello (1)"));

            BlogMlImporter.ReleaseSlug(taken, "hello");
            BlogMlImporter.ReleaseSlug(taken, "hello");
            Assert.That(BlogMlImporter.EnsureUniqueSlug("hello", taken, helper), Is.EqualTo("hello"));
        }

        [Test]
        public void Resolve_matches_archive_by_key()
        {
            (IContent root, IContent first, IContent second) = Archives();
            XDocument xDoc = Document($"<archive xmlns=\"{ArchiveSyndicationExtension.Namespace}\" key=\"{SecondKey:D}\" name=\"Wrong\" />");

            Assert.That(BlogMlImporter.ResolveArchiveForPost(root, [first, second], Marker(xDoc)), Is.SameAs(second));
        }

        [TestCase("0f8fad5b-d9cb-469f-a165-70867728950e", "archive b")]
        [TestCase("not-a-guid", "archive b")]
        [TestCase("not-a-guid", " Archive B ")]
        public void Resolve_matches_archive_by_trimmed_name_ignoring_case_when_key_unknown(string key, string name)
        {
            (IContent root, IContent first, IContent second) = Archives();
            XDocument xDoc = Document($"<archive xmlns=\"{ArchiveSyndicationExtension.Namespace}\" key=\"{key}\" name=\"{name}\" />");

            Assert.That(BlogMlImporter.ResolveArchiveForPost(root, [first, second], Marker(xDoc)), Is.SameAs(second));
        }

        [Test]
        public void Resolve_returns_root_when_named_archive_does_not_exist()
        {
            (IContent root, IContent first, IContent second) = Archives();
            XDocument xDoc = Document($"<archive xmlns=\"{ArchiveSyndicationExtension.Namespace}\" key=\"{Guid.NewGuid():D}\" name=\"Elsewhere\" />");

            Assert.That(BlogMlImporter.ResolveArchiveForPost(root, [first, second], Marker(xDoc)), Is.SameAs(root));
        }

        [Test]
        public void Resolve_without_archive_element_keeps_first_archive_fallback()
        {
            (IContent root, IContent first, IContent second) = Archives();

            Assert.That(BlogMlImporter.ResolveArchiveForPost(root, [first, second], null), Is.SameAs(first));
            Assert.That(BlogMlImporter.ResolveArchiveForPost(root, [], null), Is.SameAs(root));
        }

        [Test]
        public void Exported_posts_round_trip_to_their_own_archive()
        {
            (IContent root, IContent first, IContent second) = Archives();
            XDocument xDoc = Export(first, second);

            IContent[] archives = [first, second];
            Assert.That(BlogMlImporter.BuildPostElementLookup(xDoc).ById, Has.Count.EqualTo(2));
            Assert.That(BlogMlImporter.ResolveArchiveForPost(root, archives, Marker(xDoc, "post-a")), Is.SameAs(first));
            Assert.That(BlogMlImporter.ResolveArchiveForPost(root, archives, Marker(xDoc, "post-b")), Is.SameAs(second));
        }

        [Test]
        public void Exported_posts_round_trip_by_name_when_import_keys_differ()
        {
            (IContent root, IContent first, IContent second) = Archives();
            XDocument xDoc = Export(first, second);

            var importFirst = new Mock<IContent>();
            importFirst.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            importFirst.SetupGet(x => x.Name).Returns("ARCHIVE A");
            var importSecond = new Mock<IContent>();
            importSecond.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            importSecond.SetupGet(x => x.Name).Returns("archive b");

            IContent[] archives = [importFirst.Object, importSecond.Object];
            Assert.That(BlogMlImporter.ResolveArchiveForPost(root, archives, Marker(xDoc, "post-a")), Is.SameAs(importFirst.Object));
            Assert.That(BlogMlImporter.ResolveArchiveForPost(root, archives, Marker(xDoc, "post-b")), Is.SameAs(importSecond.Object));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task CreateArchiveNode_saves_and_publishes_only_when_publishAll(bool publishAll)
        {
            (Mock<IContentService> contentService, BlogMlImporter sut, IContent root, IContent archive) = ImporterWithArchiveCreate();
            contentService.Setup(x => x.Save(archive, 7, null)).Returns(OperationResult.Succeed(new EventMessages()));
            contentService
                .Setup(x => x.Publish(archive, It.Is<string[]>(c => c.Length == 1 && c[0] == "*"), 7))
                .Returns(new PublishResult(new EventMessages(), archive));

            IContent result = await sut.CreateArchiveNodeAsync(7, root, "Archive A", publishAll, ArchiveType());

            Assert.That(result, Is.SameAs(archive));
            contentService.Verify(x => x.Save(archive, 7, null), Times.Once);
            contentService.Verify(
                x => x.Publish(archive, It.Is<string[]>(c => c.Length == 1 && c[0] == "*"), 7),
                publishAll ? Times.Once() : Times.Never());
        }

        [Test]
        public async Task GetOrCreateArchiveNode_creates_once_for_names_differing_only_in_case_or_whitespace()
        {
            (Mock<IContentService> contentService, BlogMlImporter sut, IContent root, IContent archive) = ImporterWithArchiveCreate();
            contentService.Setup(x => x.Save(archive, 7, null)).Returns(OperationResult.Succeed(new EventMessages()));
            var created = new Dictionary<string, IContent>(StringComparer.OrdinalIgnoreCase);

            IContent first = await sut.GetOrCreateArchiveNodeAsync(
                created, 7, root, new ArchiveSyndicationExtension { ArchiveName = "Archive A" }, false, ArchiveType());
            IContent second = await sut.GetOrCreateArchiveNodeAsync(
                created, 7, root, new ArchiveSyndicationExtension { ArchiveName = " archive a " }, false, ArchiveType());

            Assert.That(first, Is.SameAs(archive));
            Assert.That(second, Is.SameAs(archive));
            contentService.Verify(x => x.Save(archive, 7, null), Times.Once);
        }

        [Test]
        public void CreateArchiveNode_throws_when_save_fails()
        {
            (Mock<IContentService> contentService, BlogMlImporter sut, IContent root, IContent archive) = ImporterWithArchiveCreate();
            contentService
                .Setup(x => x.Save(archive, 7, null))
                .Returns(new OperationResult(OperationResultType.FailedCancelledByEvent, new EventMessages()));

            Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateArchiveNodeAsync(7, root, "Archive A", true, ArchiveType()));
            contentService.Verify(x => x.Publish(It.IsAny<IContent>(), It.IsAny<string[]>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void CreateArchiveNode_throws_when_publish_fails()
        {
            (Mock<IContentService> contentService, BlogMlImporter sut, IContent root, IContent archive) = ImporterWithArchiveCreate();
            contentService.Setup(x => x.Save(archive, 7, null)).Returns(OperationResult.Succeed(new EventMessages()));
            contentService
                .Setup(x => x.Publish(archive, It.IsAny<string[]>(), 7))
                .Returns(new PublishResult(PublishResultType.FailedPublishCancelledByEvent, new EventMessages(), archive));

            Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateArchiveNodeAsync(7, root, "Archive A", true, ArchiveType()));
        }

        [Test]
        public void Skipped_existing_post_keeps_slug_taken_so_new_post_is_suffixed()
        {
            var helper = new DefaultShortStringHelper(new DefaultShortStringHelperConfig());
            IContent existing = Mock.Of<IContent>(x => x.Name == "Hello");
            Dictionary<string, int> taken = BlogMlImporter.CountSlugs([existing], helper, null);

            // overwrite=false skips before ReleaseSlug, so the existing slug is still counted
            Assert.That(BlogMlImporter.EnsureUniqueSlug("hello", taken, helper), Is.EqualTo("hello (1)"));
        }

        [TestCase(null, "a1", "a1")]
        [TestCase("", "a1", "a1")]
        [TestCase("  ", "a1", "a1")]
        [TestCase("Ann", "a1", "Ann")]
        [TestCase(null, null, null)]
        public async Task ProcessSingleAuthor_falls_back_to_id_and_skips_when_nameless(string? title, string? id, string? expected)
        {
            (_, BlogMlImporter sut, _, _) = ImporterWithArchiveCreate();
            IContent existing = Mock.Of<IContent>(x => x.Name == expected);
            var byName = new Dictionary<string, IContent>(StringComparer.OrdinalIgnoreCase);
            if (expected is not null)
            {
                byName[expected] = existing;
            }

            var author = new BlogMLAuthor { Id = id! };
            if (title is not null)
            {
                author.Title = new BlogMLTextConstruct(title);
            }

            var result = await sut.ProcessSingleAuthorAsync(1, author, Mock.Of<IContent>(), ArchiveType(), byName);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void FindExistingPost_matches_import_id_when_slug_dictionary_is_another_archives()
        {
            var helper = new DefaultShortStringHelper(new DefaultShortStringHelperConfig());
            IContent byId = Mock.Of<IContent>(x => x.Name == "Hello World");
            var byImportId = new Dictionary<string, IContent> { ["p1"] = byId };
            var otherArchiveSlugs = new Dictionary<string, IContent>(StringComparer.OrdinalIgnoreCase);

            Assert.That(
                BlogMlImporter.FindExistingPost(byImportId, otherArchiveSlugs, new BlogMLPost { Id = "p1", Name = new BlogMLTextConstruct("Hello World") }, helper),
                Is.SameAs(byId));
        }

        [Test]
        public void GetImportFileSummary_counts_posts_and_absolute_attachment_hosts_only()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "articulate-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ArticulateTempFileSystem tempFs = TempFileSystem(tempRoot);
                const string xml = "<blog xmlns=\"http://www.blogml.com/2006/09/BlogML\" date-created=\"2020-01-01T00:00:00\"><title type=\"text\">Blog</title><posts>" +
                    "<post id=\"p1\"><title type=\"text\">One</title><attachments>" +
                    "<attachment url=\"/rel/a.png\" external-uri=\"https://b.example/a.png\" />" +
                    "<attachment url=\"/rel/b.png\" external-uri=\"/relative/b.png\" />" +
                    "<attachment url=\"/rel/c.png\" />" +
                    "</attachments></post>" +
                    "<post id=\"p2\"><title type=\"text\">Two</title><attachments>" +
                    "<attachment url=\"/rel/d.png\" external-uri=\"https://A.example/d.png\" />" +
                    "<attachment url=\"/rel/e.png\" external-uri=\"https://a.example/e.png\" />" +
                    "</attachments></post></posts></blog>";
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
                {
                    tempFs.AddFile("summary.xml", stream, true);
                }

                (_, BlogMlImporter sut, _, _) = ImporterWithArchiveCreate(tempFs);
                BlogMlImportFileSummary summary = sut.GetImportFileSummary("summary.xml");

                Assert.That(summary.PostCount, Is.EqualTo(2));
                Assert.That(summary.ExternalImageCount, Is.EqualTo(3));
                Assert.That(summary.ExternalHosts, Is.EqualTo(new[] { "a.example", "b.example" }));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void Extension_round_trips_inside_post_and_load_without_marker_is_false()
        {
            var post = new XElement(XName.Get("post", "http://www.blogml.com/2006/09/BlogML"));
            using (XmlWriter writer = post.CreateWriter())
            {
                new ArchiveSyndicationExtension { ArchiveKey = FirstKey, ArchiveName = "Blog" }.WriteTo(writer);
            }

            var loaded = new ArchiveSyndicationExtension();
            Assert.That(loaded.Load(post.CreateNavigator()), Is.True);
            Assert.That(loaded.ArchiveKey, Is.EqualTo(FirstKey));
            Assert.That(loaded.ArchiveName, Is.EqualTo("Blog"));
            Assert.That(new ArchiveSyndicationExtension().Load(new XElement(post.Name).CreateNavigator()), Is.False);
        }

        private static (Mock<IContentService> ContentService, BlogMlImporter Sut, IContent Root, IContent Archive) ImporterWithArchiveCreate(
            ArticulateTempFileSystem? tempFs = null,
            IUserService? userService = null)
        {
            var archiveType = new Mock<IContentType>();
            archiveType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateArchive);
            var contentTypeService = new Mock<IContentTypeService>();
            contentTypeService.Setup(x => x.Get(ArticulateConstants.ContentType.ArticulateArchive)).Returns(archiveType.Object);

            IContent root = new Mock<IContent>().Object;
            IContent archive = new Mock<IContent>().Object;
            var contentService = new Mock<IContentService>();
            contentService.Setup(x => x.Create("Archive A", root, ArticulateConstants.ContentType.ArticulateArchive, -1)).Returns(archive);

#pragma warning disable SA1111 // closing paren after the lane-specific trailing argument
            var sut = new BlogMlImporter(
                new DisqusXmlExporter(Mock.Of<IPublishedUrlProvider>(), NullLogger<DisqusXmlExporter>.Instance, Mock.Of<IArticulateMarkdownConverter>()),
                contentService.Object,
                contentTypeService.Object,
                userService ?? Mock.Of<IUserService>(),
                NullLogger<BlogMlImporter>.Instance,
                Mock.Of<IDataTypeService>(),
                Mock.Of<ISqlContext>(),
                Mock.Of<IScopeProvider>(),
                Mock.Of<ILanguageService>(),
                new PropertyEditorCollection(new DataEditorCollection(() => [])),
                Mock.Of<IJsonSerializer>(),
                tempFs ?? TempFileSystem(Path.GetTempPath()),
                Mock.Of<IArticulateImportMediaService>(),
                Mock.Of<IHtmlSanitizer>(),
                Mock.Of<IShortStringHelper>(),
                Microsoft.Extensions.Options.Options.Create(new ArticulateOptions()),
                Microsoft.Extensions.Options.Options.Create(new ArticulateCommentsOptions())
#if UMBRACO_18_OR_GREATER
                ,
                Mock.Of<Umbraco.Cms.Core.Services.IIdKeyMap>()
#endif
            );
#pragma warning restore SA1111
            return (contentService, sut, root, archive);
        }

        private static ArticulateTempFileSystem TempFileSystem(string tempRoot)
        {
            var io = new Mock<IIOHelper>();
            io.Setup(i => i.PathStartsWith(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<char[]>()))
                .Returns<string, string, char[]>((path, root, _) => path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
            var hosting = new Mock<IHostingEnvironment>();
#pragma warning disable CS0618 // PhysicalFileSystem still resolves paths through these obsolete members.
            hosting.SetupGet(h => h.LocalTempPath).Returns(tempRoot);
            hosting.Setup(h => h.MapPathContentRoot(It.IsAny<string>())).Returns<string>(p => p);
            hosting.Setup(h => h.MapPathWebRoot(It.IsAny<string>())).Returns<string>(p => p);
#pragma warning restore CS0618
            return new ArticulateTempFileSystem(
                io.Object,
                hosting.Object,
                NullLogger<ArticulateTempFileSystem>.Instance);
        }

        private static XDocument Export(IContent first, IContent second)
        {
            var doc = new BlogMLDocument
            {
                Title = new BlogMLTextConstruct("Blog"),
                RootUrl = new Uri("http://localhost/"),
                GeneratedOn = DateTime.UtcNow
            };
            foreach ((string id, IContent archive) in new[] { ("post-a", first), ("post-b", second) })
            {
                var child = new Mock<IContent>();
                child.SetupGet(x => x.Key).Returns(Guid.NewGuid());
                child.SetupGet(x => x.Name).Returns(id);
                BlogMLPost post = BlogMlExporter.CreateBlogMlPost(child.Object, archive, "<p>x</p>", new Uri("/" + id, UriKind.Relative));
                post.Id = id;
                _ = doc.AddPost(post);
            }

            using var stream = new MemoryStream();
            doc.Save(stream);
            stream.Position = 0;
            return XDocument.Load(stream);
        }

        private static (IContent Root, IContent First, IContent Second) Archives()
        {
            var root = new Mock<IContent>();
            root.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            var first = new Mock<IContent>();
            first.SetupGet(x => x.Key).Returns(FirstKey);
            first.SetupGet(x => x.Name).Returns("Archive A");
            var second = new Mock<IContent>();
            second.SetupGet(x => x.Key).Returns(SecondKey);
            second.SetupGet(x => x.Name).Returns("Archive B");
            return (root.Object, first.Object, second.Object);
        }

        private static ArchiveSyndicationExtension? Marker(XDocument xDoc, string postId = "post-1")
        {
            (Dictionary<string, XElement> byId, _) = BlogMlImporter.BuildPostElementLookup(xDoc);
            var marker = new ArchiveSyndicationExtension();
            return byId.TryGetValue(postId, out XElement? post) && marker.Load(post.CreateNavigator()) ? marker : null;
        }

        private static IContentType ArchiveType() => Mock.Of<IContentType>(x => x.Alias == ArticulateConstants.ContentType.ArticulateArchive);

        private static XDocument Document(string postChildren) =>
            XDocument.Parse($"<blog xmlns=\"http://www.blogml.com/2006/09/BlogML\"><posts><post id=\"post-1\">{postChildren}</post></posts></blog>");

        private static string Write(ArchiveSyndicationExtension extension)
        {
            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
            {
                extension.WriteTo(writer);
            }

            return sb.ToString();
        }
    }
}
