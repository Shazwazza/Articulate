#nullable enable
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;
using Articulate.ImportExport;
using Articulate.Options;
using Articulate.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
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
    internal class BlogMlImporterTests
    {
        private static readonly XNamespace BlogMl = "http://www.blogml.com/2006/09/BlogML";

        [Test]
        public async Task Import_keeps_the_first_definition_of_a_repeated_author_id()
        {
            using var fixture = new ImportFixture();

            await fixture.ImportAsync(
                [ImportFixture.Author("author-1", "First author"), ImportFixture.Author("author-1", "Repeated id")],
                ImportFixture.Post("post-1", authorId: "author-1"));

            Assert.That(fixture.Authors.Select(x => x.Name), Is.EqualTo(new[] { "First author" }));
            Assert.That(fixture.Posts.Single().GetValue<string>("author"), Is.EqualTo("First author"));
        }

        [Test]
        public async Task Import_skips_an_author_without_a_title()
        {
            using var fixture = new ImportFixture();

            await fixture.ImportAsync(
                [ImportFixture.Author("author-1", name: null)],
                ImportFixture.Post("post-1", authorId: "author-1"));

            Assert.That(fixture.Authors, Is.Empty);
            Assert.That(fixture.Posts.Single().GetValue<string>("author"), Is.Null.Or.Empty);
        }

        [Test]
        public async Task Import_continues_when_post_url_has_no_path_segment()
        {
            using var fixture = new ImportFixture();

            await fixture.ImportAsync(
                [],
                ImportFixture.Post("root-url", slug: null, url: "/"),
                ImportFixture.Post("following-post"));

            Assert.That(fixture.Posts.Select(x => x.GetValue<string>("importId")), Is.EquivalentTo(new[] { "root-url", "following-post" }));
        }

        // Real content/property models; only filesystem, query and service boundaries are substituted.
        private sealed class ImportFixture : IDisposable
        {
            private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "articulate-import-" + Guid.NewGuid().ToString("N"));
            private readonly Dictionary<string, ContentType> _types = new(StringComparer.Ordinal);
            private readonly Dictionary<IQuery<IContent>, Func<IContent, bool>> _filters = [];
            private readonly Mock<IContentService> _contentService = new();
            private readonly ArticulateTempFileSystem _tempFiles;
            private readonly BlogMlImporter _importer;
            private readonly IContent _root;
            private int _nextId = 100;

            public ImportFixture()
            {
                var helper = new DefaultShortStringHelper(new DefaultShortStringHelperConfig());
                var dataType = new Mock<IDataType>();
                dataType.SetupGet(x => x.Id).Returns(1);
                dataType.SetupGet(x => x.Key).Returns(Guid.NewGuid());
                dataType.SetupGet(x => x.HasIdentity).Returns(true);
                dataType.SetupGet(x => x.EditorAlias).Returns("Umbraco.TextBox");
                dataType.SetupGet(x => x.DatabaseType).Returns(ValueStorageType.Ntext);
                dataType.SetupGet(x => x.ConfigurationObject).Returns(new TagConfiguration { StorageType = TagsStorageType.Csv });
                var dataTypes = new Mock<IDataTypeService>();
#if !UMBRACO_18_OR_GREATER
#pragma warning disable CS0618 // Exercise the v17 tags API, which still resolves data types by ID.
                dataTypes.Setup(x => x.GetDataType(It.IsAny<int>())).Returns(dataType.Object);
#pragma warning restore CS0618
#endif
                dataTypes.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(dataType.Object);
                var typeService = new Mock<IContentTypeService>();
                foreach (string alias in new[] { ArticulateConstants.ContentType.Articulate, ArticulateConstants.ContentType.ArticulateArchive, ArticulateConstants.ContentType.ArticulateRichText, ArticulateConstants.ContentType.ArticulateAuthors, ArticulateConstants.ContentType.ArticulateAuthor })
                {
                    var type = new ContentType(helper, -1) { Id = _types.Count + 1, Alias = alias, Name = alias };
                    if (alias == ArticulateConstants.ContentType.ArticulateRichText)
                    {
                        foreach (string property in new[] { "umbracoUrlName", "importId", "publishedDate", "excerpt", "richText", "enableComments", "author", "tags", "categories" })
                        {
                            type.AddPropertyType(new PropertyType(helper, dataType.Object, property));
                        }
                    }

                    _types.Add(alias, type);
                    typeService.Setup(x => x.Get(alias)).Returns(type);
                }

                _root = AddContent("Blog", -1, ArticulateConstants.ContentType.Articulate);
                _ = AddContent("Authors", _root.Id, ArticulateConstants.ContentType.ArticulateAuthors);
                _contentService.Setup(x => x.GetById(It.IsAny<Guid>())).Returns<Guid>(key => Nodes.SingleOrDefault(x => x.Key == key));
                _contentService.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<IContent>(), It.IsAny<string>(), It.IsAny<int>()))
                    .Returns<string, IContent, string, int>((name, parent, alias, _) => new Content(name, parent.Id, _types[alias]));
                _contentService.Setup(x => x.Save(It.IsAny<IContent>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
                    .Returns<IContent, int?, ContentScheduleCollection?>((content, _, _) =>
                    {
                        // Matches Umbraco's DocumentRepository, which refuses to save a node without a name.
                        if (string.IsNullOrWhiteSpace(content.Name))
                        {
                            throw new InvalidOperationException("Cannot save content with an empty name.");
                        }

                        if (content.Id == 0)
                        {
                            content.Id = _nextId++;
                            Nodes.Add(content);
                        }

                        return OperationResult.Succeed(new EventMessages());
                    });
                _contentService.Setup(x => x.Publish(It.IsAny<IContent>(), It.IsAny<string[]>(), It.IsAny<int>()))
                    .Returns<IContent, string[], int>((content, _, _) => new PublishResult(new EventMessages(), content));
                var sql = new Mock<ISqlContext>();
                sql.Setup(x => x.Query<IContent>()).Returns(() =>
                {
                    var query = new Mock<IQuery<IContent>>();
                    query.Setup(x => x.Where(It.IsAny<Expression<Func<IContent, bool>>>()))
                        .Returns<Expression<Func<IContent, bool>>>(predicate =>
                        {
                            _filters[query.Object] = predicate.Compile();
                            return query.Object;
                        });
                    return query.Object;
                });
                _contentService.Setup(x => x.GetPagedOfType(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out It.Ref<long>.IsAny, It.IsAny<IQuery<IContent>>(), It.IsAny<Ordering?>()))
                    .Returns((int type, long _, int _, out long total, IQuery<IContent> filter, Ordering? _) =>
                    {
                        IContent[] found = [.. Nodes.Where(x => x.ContentType.Id == type && _filters[filter](x))];
                        total = found.Length;
                        return found;
                    });
                _contentService.Setup(x => x.GetPagedChildren(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out It.Ref<long>.IsAny, null, It.IsAny<IQuery<IContent>?>(), It.IsAny<Ordering?>(), true))
                    .Returns((int parent, long _, int _, out long total, string[]? _, IQuery<IContent>? filter, Ordering? _, bool _) =>
                    {
                        IContent[] found = [.. Nodes.Where(x => x.ParentId == parent && (filter is null || _filters[filter](x)))];
                        total = found.Length;
                        return found;
                    });
                var scopes = new Mock<IScopeProvider>();
                scopes.Setup(x => x.CreateScope(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<Umbraco.Cms.Core.Scoping.RepositoryCacheMode>(), It.IsAny<IEventDispatcher?>(), It.IsAny<IScopedNotificationPublisher?>(), It.IsAny<bool?>(), It.IsAny<bool>(), It.IsAny<bool>())).Returns(Mock.Of<IScope>());
                var io = new Mock<IIOHelper>();
                io.Setup(x => x.PathStartsWith(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<char[]>())).Returns<string, string, char[]>((path, root, _) => path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
                var hosting = new Mock<IHostingEnvironment>();
#pragma warning disable CS0618
                hosting.SetupGet(x => x.LocalTempPath).Returns(_tempRoot);

                // A relative temp root resolves through MapPathContentRoot("~"); keep it under _tempRoot too.
                hosting.Setup(x => x.MapPathContentRoot(It.IsAny<string>())).Returns(_tempRoot);
#pragma warning restore CS0618
                _tempFiles = new ArticulateTempFileSystem(io.Object, hosting.Object, NullLogger<ArticulateTempFileSystem>.Instance);
                var sanitizer = new Mock<IHtmlSanitizer>();
                sanitizer.Setup(x => x.Sanitize(It.IsAny<string>())).Returns<string>(x => x);
#pragma warning disable SA1111
                _importer = new BlogMlImporter(
                    new DisqusXmlExporter(Mock.Of<IPublishedUrlProvider>(), NullLogger<DisqusXmlExporter>.Instance, Mock.Of<IArticulateMarkdownConverter>()),
                    _contentService.Object,
                    typeService.Object,
                    Mock.Of<IUserService>(),
                    NullLogger<BlogMlImporter>.Instance,
                    dataTypes.Object,
                    sql.Object,
                    scopes.Object,
                    Mock.Of<ILanguageService>(),
                    new PropertyEditorCollection(new DataEditorCollection(() => [])),
                    Mock.Of<IJsonSerializer>(),
                    _tempFiles,
                    Mock.Of<IArticulateImportMediaService>(),
                    sanitizer.Object,
                    Microsoft.Extensions.Options.Options.Create(new ArticulateOptions()),
                    Microsoft.Extensions.Options.Options.Create(new ArticulateCommentsOptions())
#if UMBRACO_18_OR_GREATER
                    ,
                    Mock.Of<IIdKeyMap>()
#endif
                );
#pragma warning restore SA1111
            }

            public List<IContent> Nodes { get; } = [];

            public IEnumerable<IContent> Posts => Nodes.Where(x => x.ContentType.Alias == ArticulateConstants.ContentType.ArticulateRichText);

            public IEnumerable<IContent> Authors => Nodes.Where(x => x.ContentType.Alias == ArticulateConstants.ContentType.ArticulateAuthor);

            public static XElement Post(string id, string? slug = "hello", string url = "/hello.aspx", string? authorId = null)
            {
                var post = new XElement(BlogMl + "post", new XAttribute("id", id), new XAttribute("date-created", "2020-01-01T00:00:00"), new XAttribute("post-url", url), new XElement(BlogMl + "title", "Title"), new XElement(BlogMl + "content", new XAttribute("type", "text"), "Body"));
                if (slug is not null)
                {
                    post.Add(new XElement(BlogMl + "post-name", slug));
                }

                if (authorId is not null)
                {
                    post.Add(new XElement(BlogMl + "authors", new XElement(BlogMl + "author", new XAttribute("ref", authorId))));
                }

                return post;
            }

            public static XElement Author(string id, string? name)
            {
                var author = new XElement(BlogMl + "author", new XAttribute("id", id));
                if (name is not null)
                {
                    author.Add(new XElement(BlogMl + "title", new XAttribute("type", "text"), name));
                }

                return author;
            }

            public Task<ImportResponseDto> ImportAsync(XElement[] authors, params XElement[] posts)
            {
                var document = new XDocument(new XElement(
                    BlogMl + "blog",
                    new XAttribute("date-created", "2020-01-01T00:00:00"),
                    new XElement(BlogMl + "title", "Blog"),
                    new XElement(BlogMl + "authors", authors),
                    new XElement(BlogMl + "posts", posts)));
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(document.ToString()));
                _tempFiles.AddFile("import.xml", stream, true);
                return _importer.ImportAsync(7, "import.xml", _root.Key, false, null, null, false);
            }

            public void Dispose()
            {
                if (Directory.Exists(_tempRoot))
                {
                    Directory.Delete(_tempRoot, true);
                }
            }

            private IContent AddContent(string name, int parent, string alias)
            {
                var content = new Content(name, parent, _types[alias]) { Id = _nextId++ };
                Nodes.Add(content);
                return content;
            }
        }
    }
}
