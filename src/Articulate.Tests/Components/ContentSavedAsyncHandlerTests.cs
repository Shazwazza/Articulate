#nullable enable
using Articulate.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Scoping;
using System.Data;
using IScope = Umbraco.Cms.Infrastructure.Scoping.IScope;
using IScopeProvider = Umbraco.Cms.Infrastructure.Scoping.IScopeProvider;

namespace Articulate.Tests.Components
{
    [TestFixture]
    public class ContentSavedAsyncHandlerTests
    {
        private Mock<IScopeProvider> _scopeProvider = null!;

        [SetUp]
        public void SetUp()
        {
            _scopeProvider = new Mock<IScopeProvider>();
            _scopeProvider.Setup(x => x.CreateScope(
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<RepositoryCacheMode>(),
                    It.IsAny<IEventDispatcher>(),
                    It.IsAny<IScopedNotificationPublisher>(),
                    It.IsAny<bool?>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()))
                .Returns(Mock.Of<IScope>());
        }

        [Test]
        public async Task HandleAsync_publishes_required_children_when_articulate_root_is_published()
        {
            Mock<IContentTypeService> contentTypeService = new();
            Mock<IContentService> contentService = new();
            Mock<ILanguageService> languageService = new();

            IContent root = CreateContent(
                id: 100,
                contentTypeId: 1,
                alias: ArticulateConstants.ContentType.Articulate,
                published: true);
            IContent archive = CreateContent(
                id: 101,
                contentTypeId: 10,
                alias: ArticulateConstants.ContentType.ArticulateArchive,
                published: false);
            IContent authors = CreateContent(
                id: 102,
                contentTypeId: 11,
                alias: ArticulateConstants.ContentType.ArticulateAuthors,
                published: false);

            contentTypeService
                .Setup(x => x.Get(ArticulateConstants.ContentType.ArticulateArchive))
                .Returns(CreateContentType(10, ArticulateConstants.ContentType.ArticulateArchive));
            contentTypeService
                .Setup(x => x.Get(ArticulateConstants.ContentType.ArticulateAuthors))
                .Returns(CreateContentType(11, ArticulateConstants.ContentType.ArticulateAuthors));

            SetupGetPagedChildren(contentService, [archive, authors]);
            languageService.Setup(x => x.GetDefaultIsoCodeAsync()).ReturnsAsync("en-US");

            contentService
                .Setup(x => x.Publish(archive, It.Is<string[]>(cultures => cultures.Length == 1 && cultures[0] == "*"), -1))
                .Returns(new PublishResult(new EventMessages(), archive));
            contentService
                .Setup(x => x.Publish(authors, It.Is<string[]>(cultures => cultures.Length == 1 && cultures[0] == "*"), -1))
                .Returns(new PublishResult(new EventMessages(), authors));

            ArticulateRootContentLifecycleHandler sut = new(
                contentTypeService.Object,
                contentService.Object,
                languageService.Object,
                NullLogger<ArticulateRootContentLifecycleHandler>.Instance,
                _scopeProvider.Object);

            await sut.HandleAsync(
                new ContentPublishedNotification([root], new EventMessages()),
                CancellationToken.None);

            contentService.Verify(
                x => x.Publish(archive, It.Is<string[]>(cultures => cultures.Length == 1 && cultures[0] == "*"), -1),
                Times.Once);
            contentService.Verify(
                x => x.Publish(authors, It.Is<string[]>(cultures => cultures.Length == 1 && cultures[0] == "*"), -1),
                Times.Once);
            contentService.Verify(x => x.Save(It.IsAny<IContent>()), Times.Never);
        }

        private static void SetupGetPagedChildren(Mock<IContentService> contentService, IEnumerable<IContent> children)
        {
            contentService
                .Setup(x => x.GetPagedChildren(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<int>(),
                    out It.Ref<long>.IsAny,
                    null,
                    null,
                    null,
                    true))
                .Returns((
                    int _,
                    long _,
                    int _,
                    out long total,
                    string[]? _,
                    IQuery<IContent>? _,
                    Ordering? _,
                    bool _) =>
                {
                    var items = children.ToList();
                    total = items.Count;
                    return items;
                });
        }

        private static IContentType CreateContentType(int id, string alias, bool variesByCulture = false)
        {
            Mock<IContentType> contentType = new();
            contentType.SetupGet(x => x.Id).Returns(id);
            contentType.SetupGet(x => x.Alias).Returns(alias);
            contentType.SetupGet(x => x.Variations).Returns(variesByCulture ? ContentVariation.Culture : ContentVariation.Nothing);
            return contentType.Object;
        }

        private static IContent CreateContent(int id, int contentTypeId, string alias, bool published)
        {
            Mock<IContent> content = new();
            content.SetupGet(x => x.Id).Returns(id);
            content.SetupGet(x => x.ContentTypeId).Returns(contentTypeId);
            content.SetupGet(x => x.Published).Returns(published);
            content.SetupGet(x => x.ContentType).Returns(CreateSimpleContentType(alias));
            return content.Object;
        }

        private static ISimpleContentType CreateSimpleContentType(string alias)
        {
            Mock<ISimpleContentType> contentType = new();
            contentType.SetupGet(x => x.Alias).Returns(alias);
            return contentType.Object;
        }
    }
}
