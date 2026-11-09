#nullable enable
using System.Linq.Expressions;
using System.Xml.Linq;
using Articulate.ImportExport;
using Articulate.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Articulate.Tests.ImportExport
{
    [TestFixture]
    internal class BlogMlExporterTests
    {
        private static readonly XNamespace BlogMl = "http://www.blogml.com/2006/09/BlogML";

        [Test]
        public void GetPostDate_returns_publishedDate_when_set()
        {
            var publishedDate = new DateTime(2014, 3, 21, 0, 56, 0);
            Mock<IContent> post = new();
            post.Setup(x => x.GetValue<DateTime>("publishedDate", null, null, false)).Returns(publishedDate);
            post.SetupGet(x => x.CreateDate).Returns(new DateTime(2023, 11, 2, 15, 50, 9));

            Assert.That(BlogMlExporter.GetPostDate(post.Object), Is.EqualTo(publishedDate));
        }

        [Test]
        public void GetPostDate_falls_back_to_CreateDate_when_publishedDate_is_not_set()
        {
            var createDate = new DateTime(2023, 11, 2, 15, 50, 9);
            Mock<IContent> post = new();
            post.Setup(x => x.GetValue<DateTime>("publishedDate", null, null, false)).Returns(default(DateTime));
            post.SetupGet(x => x.CreateDate).Returns(createDate);

            Assert.That(BlogMlExporter.GetPostDate(post.Object), Is.EqualTo(createDate));
        }

        [Test]
        public async Task Export_serializes_only_categories_used_by_exported_published_posts()
        {
            using var fixture = new ExportFixture();
            IContent archive = fixture.AddArchive();
            IContent first = fixture.AddPost(archive, "First", published: true);
            IContent second = fixture.AddPost(archive, "Second", published: true);
            IContent draft = fixture.AddPost(archive, "Draft");

            // NodeCount > 0: every tag is in use somewhere, so only the export's own posts can exclude one.
            var shared = new Tag(1, "categories", "Shared") { NodeCount = 2 };
            var secondOnly = new Tag(2, "categories", "Second only") { NodeCount = 1 };
            var draftOnly = new Tag(3, "categories", "Draft only") { NodeCount = 1 };
            var otherBlog = new Tag(4, "categories", "Other blog") { NodeCount = 1 };
            fixture.Categories[first.Id] = [shared];
            fixture.Categories[second.Id] = [shared, secondOnly];
            fixture.Categories[draft.Id] = [draftOnly];
            fixture.AllCategories = [shared, secondOnly, draftOnly, otherBlog];

            XDocument document = await fixture.ExportAsync();

            XElement root = document.Root!;
            Assert.That(root.Element(BlogMl + "categories")?.Elements(BlogMl + "category").Select(x => (string?)x.Attribute("id")), Is.EquivalentTo(new[] { "1", "2" }));
            Assert.That(root.Element(BlogMl + "posts")?.Elements(BlogMl + "post").Select(x => (string?)x.Attribute("id")), Is.EquivalentTo(new[] { first.Key.ToString(), second.Key.ToString() }));
            Assert.That(document.Descendants(BlogMl + "post").SelectMany(x => x.Element(BlogMl + "categories")!.Elements()).Select(x => (string?)x.Attribute("ref")), Is.EquivalentTo(new[] { "1", "1", "2" }));
        }

        private sealed class ExportFixture : IDisposable
        {
            private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "articulate-export-" + Guid.NewGuid().ToString("N"));
            private readonly List<IContent> _nodes = [];
            private readonly Dictionary<IQuery<IContent>, Func<IContent, bool>> _filters = [];
            private readonly Dictionary<string, IContentType> _types = new(StringComparer.Ordinal);
            private readonly IContent _root;
            private int _nextId = 100;

            public ExportFixture()
            {
                var helper = new DefaultShortStringHelper(new DefaultShortStringHelperConfig());
                var categoryDataType = new Mock<IDataType>();
                categoryDataType.SetupGet(x => x.ConfigurationObject).Returns(new TagConfiguration { Group = "categories" });
                var tagDataType = new Mock<IDataType>();
                tagDataType.SetupGet(x => x.ConfigurationObject).Returns(new TagConfiguration { Group = "tags" });

                var types = new Mock<IContentTypeService>();
                IContentType rootType = CreateType(helper, ArticulateConstants.ContentType.Articulate, 1);
                IContentType archiveType = CreateType(helper, ArticulateConstants.ContentType.ArticulateArchive, 2);
                IContentType postType = CreateType(helper, ArticulateConstants.ContentType.ArticulateRichText, 3);
                IContentType authorsType = CreateType(helper, ArticulateConstants.ContentType.ArticulateAuthors, 4);
                foreach (IContentType type in new[] { rootType, archiveType, postType, authorsType })
                {
                    _types[type.Alias] = type;
                    types.Setup(x => x.Get(type.Alias)).Returns(type);
                }

                var dataTypes = new Mock<IDataTypeService>();
                dataTypes.Setup(x => x.GetAsync("Articulate Categories")).ReturnsAsync(categoryDataType.Object);
                dataTypes.Setup(x => x.GetAsync("Articulate Tags")).ReturnsAsync(tagDataType.Object);

                _root = NewContent("Blog", -1, rootType);
                _root.SetValue("blogTitle", "Blog");
                _root.SetValue("blogDescription", "Description");
                _nodes.Add(_root);

                ContentService.Setup(x => x.GetById(It.IsAny<Guid>())).Returns<Guid>(key => _nodes.SingleOrDefault(x => x.Key == key));
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
                ContentService.Setup(x => x.GetPagedDescendants(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out It.Ref<long>.IsAny, It.IsAny<IQuery<IContent>?>(), It.IsAny<Ordering?>()))
                    .Returns((int parent, long page, int size, out long total, IQuery<IContent>? filter, Ordering? _) =>
                    {
                        IContent[] found = [.. _nodes.Where(x => IsDescendant(x, parent) && (filter is null || _filters[filter](x)))];
                        total = found.Length;
                        return found.Skip((int)page * size).Take(size);
                    });
                ContentService.Setup(x => x.GetPagedChildren(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out It.Ref<long>.IsAny, It.IsAny<string[]?>(), It.IsAny<IQuery<IContent>?>(), It.IsAny<Ordering?>(), It.IsAny<bool>()))
                    .Returns((int parent, long page, int size, out long total, string[]? _, IQuery<IContent>? _, Ordering? _, bool _) =>
                    {
                        IContent[] found = [.. _nodes.Where(x => x.ParentId == parent)];
                        total = found.Length;
                        return found.Skip((int)page * size).Take(size);
                    });

                var io = new Mock<IIOHelper>();
                io.Setup(x => x.PathStartsWith(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<char[]>()))
                    .Returns<string, string, char[]>((path, root, _) => path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
                var hosting = new Mock<IHostingEnvironment>();
#pragma warning disable CS0618
                hosting.SetupGet(x => x.LocalTempPath).Returns(_tempRoot);

                // A relative temp root resolves through MapPathContentRoot("~"); keep it under _tempRoot too.
                hosting.Setup(x => x.MapPathContentRoot(It.IsAny<string>())).Returns(_tempRoot);
                hosting.Setup(x => x.MapPathWebRoot(It.IsAny<string>())).Returns<string>(x => x);
#pragma warning restore CS0618
                TempFiles = new ArticulateTempFileSystem(io.Object, hosting.Object, NullLogger<ArticulateTempFileSystem>.Instance);

                Exporter = new BlogMlExporter(
                    ContentService.Object,
                    Mock.Of<IMediaService>(),
                    types.Object,
                    dataTypes.Object,
                    TagService.Object,
                    null!,
                    TempFiles,
                    UrlProvider.Object,
                    sql.Object,
                    NullLogger<BlogMlExporter>.Instance,
                    Mock.Of<IArticulateMarkdownConverter>());
            }

            public Mock<IContentService> ContentService { get; } = new();
            public Mock<ITagService> TagService { get; } = new();
            public Mock<IPublishedUrlProvider> UrlProvider { get; } = new();
            public ArticulateTempFileSystem TempFiles { get; }
            public BlogMlExporter Exporter { get; }
            public Dictionary<int, ITag[]> Categories { get; } = [];
            public ITag[] AllCategories { get; set; } = [];

            public IContent AddArchive() => AddContent("Archive", _root.Id, ArticulateConstants.ContentType.ArticulateArchive);

            public IContent AddPost(IContent archive, string name, bool published = false)
            {
                IContent post = AddContent(name, archive.Id, ArticulateConstants.ContentType.ArticulateRichText);
                ((Content)post).Published = published;
                post.SetValue("richText", "<p>Content</p>");
                return post;
            }

            public async Task<XDocument> ExportAsync()
            {
                TagService.Setup(x => x.GetTagsForEntity(It.IsAny<int>(), It.IsAny<string>()))
                    .Returns<int, string, string?>((id, group, _) => group == "categories" && Categories.TryGetValue(id, out ITag[]? values) ? values : []);
                TagService.Setup(x => x.GetAllContentTags("categories")).Returns(() => AllCategories);
                UrlProvider.Setup(x => x.GetUrl(It.IsAny<int>(), It.IsAny<UrlMode>(), It.IsAny<string?>(), It.IsAny<Uri?>()))
                    .Returns<int, UrlMode, string?, Uri?>((id, _, _, _) => "https://example.test/" + id);

                await Exporter.ExportAsync(_root.Key, "export.xml");
                await using Stream stream = TempFiles.OpenFile("export.xml");
                return XDocument.Load(stream);
            }

            private IContent AddContent(string name, int parent, string alias)
            {
                IContentType type = _types[alias];
                IContent content = NewContent(name, parent, type);
                _nodes.Add(content);
                return content;
            }

            private IContent NewContent(string name, int parent, IContentType type)
            {
                var content = new Content(name, parent, type)
                {
                    Id = _nextId++,
                    CreateDate = DateTime.UtcNow,
                    UpdateDate = DateTime.UtcNow,
                };
                return content;
            }

            private bool IsDescendant(IContent node, int parent)
            {
                while (node.ParentId != parent)
                {
                    IContent? ancestor = _nodes.SingleOrDefault(x => x.Id == node.ParentId);
                    if (ancestor is null)
                    {
                        return false;
                    }

                    node = ancestor;
                }

                return true;
            }

            private static IContentType CreateType(DefaultShortStringHelper helper, string alias, int id)
            {
                var type = new ContentType(helper, -1) { Id = id, Alias = alias, Name = alias };
                IDataType dataType = Mock.Of<IDataType>();
                foreach (string property in new[] { "blogTitle", "blogDescription", "richText", "excerpt", "author", "publishedDate" })
                {
                    type.AddPropertyType(new PropertyType(helper, dataType, property));
                }

                return type;
            }

            public void Dispose()
            {
                if (Directory.Exists(_tempRoot))
                {
                    Directory.Delete(_tempRoot, true);
                }
            }
        }
    }
}
