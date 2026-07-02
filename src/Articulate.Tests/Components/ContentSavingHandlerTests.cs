#nullable enable
using Articulate.Components;
using Articulate.Options;
using Articulate.Services;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Articulate.Tests.Components
{
    [TestFixture]
    internal class ContentSavingHandlerTests
    {
        [Test]
        public void Handle_sets_enableComments_for_new_articulate_posts()
        {
            Mock<IPropertyType> enableCommentsProperty = new();
            enableCommentsProperty.SetupGet(x => x.Alias).Returns("enableComments");

            Mock<IPropertyType> publishedDateProperty = new();
            publishedDateProperty.SetupGet(x => x.Alias).Returns("publishedDate");

            Mock<IPropertyType> authorProperty = new();
            authorProperty.SetupGet(x => x.Alias).Returns("author");

            Mock<IContentType> contentType = new();
            contentType.SetupGet(x => x.Id).Returns(101);
            contentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateMarkdown);
            contentType.SetupGet(x => x.CompositionPropertyTypes).Returns(
                [publishedDateProperty.Object, authorProperty.Object, enableCommentsProperty.Object]);

            Mock<ISimpleContentType> simpleContentType = new();
            simpleContentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateMarkdown);

            Mock<IContent> content = new();
            content.SetupGet(x => x.ContentTypeId).Returns(101);
            content.SetupGet(x => x.ContentType).Returns(simpleContentType.Object);
            content.SetupGet(x => x.HasIdentity).Returns(false);
            content.Setup(x => x.GetValue("publishedDate", null, null, false)).Returns((object?)null);
            content.Setup(x => x.GetValue("author", null, null, false)).Returns((object?)null);

            var setValueCalls = new List<(string Alias, object? Value)>();
            content
                .Setup(x => x.SetValue(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Callback<string, object?, string?, string?>((alias, value, _, _) => setValueCalls.Add((alias, value)));

            Mock<IContentTypeService> contentTypeService = new();
            contentTypeService
                .Setup(x => x.GetMany(It.Is<int[]>(ids => ids.Length == 1 && ids[0] == 101)))
                .Returns([contentType.Object]);

            Mock<IBackOfficeSecurityAccessor> securityAccessor = new();
            securityAccessor.SetupGet(x => x.BackOfficeSecurity).Returns((IBackOfficeSecurity?)null);

            Mock<IArticulateMarkdownConverter> markdownConverter = new();
            Mock<IArticulateRichTextRenderer> richTextRenderer = new();
            var sut = new ContentSavingHandler(
                contentTypeService.Object,
                securityAccessor.Object,
                Microsoft.Extensions.Options.Options.Create(new ArticulateOptions { AutoGenerateExcerpt = false }),
                markdownConverter.Object,
                richTextRenderer.Object);

            sut.Handle(new ContentSavingNotification([content.Object], new EventMessages()));

            Assert.That(
                setValueCalls.Any(x => x is { Alias: "enableComments", Value: 1 }),
                Is.True,
                "Expected new Articulate posts to default enableComments to 1.");
        }

        [Test]
        public void Handle_does_not_set_enableComments_for_existing_articulate_posts()
        {
            Mock<IPropertyType> enableCommentsProperty = new();
            enableCommentsProperty.SetupGet(x => x.Alias).Returns("enableComments");

            Mock<IPropertyType> publishedDateProperty = new();
            publishedDateProperty.SetupGet(x => x.Alias).Returns("publishedDate");

            Mock<IPropertyType> authorProperty = new();
            authorProperty.SetupGet(x => x.Alias).Returns("author");

            Mock<IContentType> contentType = new();
            contentType.SetupGet(x => x.Id).Returns(101);
            contentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateRichText);
            contentType.SetupGet(x => x.CompositionPropertyTypes).Returns(
                [publishedDateProperty.Object, authorProperty.Object, enableCommentsProperty.Object]);

            Mock<ISimpleContentType> simpleContentType = new();
            simpleContentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateRichText);

            Mock<IContent> content = new();
            content.SetupGet(x => x.ContentTypeId).Returns(101);
            content.SetupGet(x => x.ContentType).Returns(simpleContentType.Object);
            content.SetupGet(x => x.HasIdentity).Returns(true);
            content.Setup(x => x.GetValue("publishedDate", null, null, false)).Returns(DateTime.Now);
            content.Setup(x => x.GetValue("author", null, null, false)).Returns("existing-author");

            var setValueCalls = new List<string>();
            content
                .Setup(x => x.SetValue(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Callback<string, object?, string?, string?>((alias, _, _, _) => setValueCalls.Add(alias));

            Mock<IContentTypeService> contentTypeService = new();
            contentTypeService
                .Setup(x => x.GetMany(It.Is<int[]>(ids => ids.Length == 1 && ids[0] == 101)))
                .Returns([contentType.Object]);

            Mock<IBackOfficeSecurityAccessor> securityAccessor = new();
            securityAccessor.SetupGet(x => x.BackOfficeSecurity).Returns((IBackOfficeSecurity?)null);

            Mock<IArticulateMarkdownConverter> markdownConverter = new();
            Mock<IArticulateRichTextRenderer> richTextRenderer = new();
            var sut = new ContentSavingHandler(
                contentTypeService.Object,
                securityAccessor.Object,
                Microsoft.Extensions.Options.Options.Create(new ArticulateOptions { AutoGenerateExcerpt = false }),
                markdownConverter.Object,
                richTextRenderer.Object);

            sut.Handle(new ContentSavingNotification([content.Object], new EventMessages()));

            Assert.That(
                setValueCalls,
                Does.Not.Contain("enableComments"),
                "Expected enableComments not to be set for existing content.");
        }

        [Test]
        public void Handle_does_not_set_defaults_for_non_articulate_content_type()
        {
            Mock<IContentType> contentType = new();
            contentType.SetupGet(x => x.Id).Returns(200);
            contentType.SetupGet(x => x.Alias).Returns("textPage");

            Mock<ISimpleContentType> simpleContentType = new();
            simpleContentType.SetupGet(x => x.Alias).Returns("textPage");

            Mock<IContent> content = new();
            content.SetupGet(x => x.ContentTypeId).Returns(200);
            content.SetupGet(x => x.ContentType).Returns(simpleContentType.Object);

            var setValueCalls = new List<string>();
            content
                .Setup(x => x.SetValue(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Callback<string, object?, string?, string?>((alias, _, _, _) => setValueCalls.Add(alias));

            Mock<IContentTypeService> contentTypeService = new();
            contentTypeService
                .Setup(x => x.GetMany(It.IsAny<int[]>()))
                .Returns([contentType.Object]);

            Mock<IBackOfficeSecurityAccessor> securityAccessor = new();
            Mock<IArticulateMarkdownConverter> markdownConverter = new();
            Mock<IArticulateRichTextRenderer> richTextRenderer = new();
            var sut = new ContentSavingHandler(
                contentTypeService.Object,
                securityAccessor.Object,
                Microsoft.Extensions.Options.Options.Create(new ArticulateOptions { AutoGenerateExcerpt = false }),
                markdownConverter.Object,
                richTextRenderer.Object);

            sut.Handle(new ContentSavingNotification([content.Object], new EventMessages()));

            Assert.That(setValueCalls, Is.Empty, "Expected no SetValue calls for non-Articulate content.");
        }

        [Test]
        public void Handle_generates_excerpt_from_rich_text_markup_not_raw_json()
        {
            Mock<IPropertyType> excerptProperty = new();
            excerptProperty.SetupGet(x => x.Alias).Returns("excerpt");

            Mock<IPropertyType> publishedDateProperty = new();
            publishedDateProperty.SetupGet(x => x.Alias).Returns("publishedDate");

            Mock<IPropertyType> authorProperty = new();
            authorProperty.SetupGet(x => x.Alias).Returns("author");

            Mock<IPropertyType> richTextProperty = new();
            richTextProperty.SetupGet(x => x.Alias).Returns("richText");

            Mock<IContentType> contentType = new();
            contentType.SetupGet(x => x.Id).Returns(101);
            contentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateRichText);
            contentType.SetupGet(x => x.CompositionPropertyTypes).Returns(
                [publishedDateProperty.Object, authorProperty.Object, excerptProperty.Object, richTextProperty.Object]);

            Mock<ISimpleContentType> simpleContentType = new();
            simpleContentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateRichText);

            Mock<IContent> content = new();
            content.SetupGet(x => x.ContentTypeId).Returns(101);
            content.SetupGet(x => x.ContentType).Returns(simpleContentType.Object);
            content.SetupGet(x => x.HasIdentity).Returns(true);
            content.Setup(x => x.HasProperty("richText")).Returns(true);
            content.Setup(x => x.HasProperty("excerpt")).Returns(true);
            content.Setup(x => x.GetValue("publishedDate", null, null, false)).Returns((object?)DateTime.Now);
            content.Setup(x => x.GetValue("author", null, null, false)).Returns("existing-author");
            content.Setup(x => x.GetValue("excerpt", null, null, false)).Returns((object?)null);
            content.Setup(x => x.GetValue<string>("richText", null, null, false))
                .Returns("""{"markup":"<p>Hello <strong>world</strong></p>","blocks":null}""");

            var setValueCalls = new List<(string Alias, object? Value)>();
            content
                .Setup(x => x.SetValue(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Callback<string, object?, string?, string?>((alias, value, _, _) => setValueCalls.Add((alias, value)));

            Mock<IContentTypeService> contentTypeService = new();
            contentTypeService
                .Setup(x => x.GetMany(It.Is<int[]>(ids => ids.Length == 1 && ids[0] == 101)))
                .Returns([contentType.Object]);

            Mock<IBackOfficeSecurityAccessor> securityAccessor = new();
            securityAccessor.SetupGet(x => x.BackOfficeSecurity).Returns((IBackOfficeSecurity?)null);

            Mock<IArticulateMarkdownConverter> markdownConverter = new();
            Mock<IArticulateRichTextRenderer> richTextRenderer = new();
            richTextRenderer
                .Setup(x => x.GetMarkup("""{"markup":"<p>Hello <strong>world</strong></p>","blocks":null}"""))
                .Returns("<p>Hello <strong>world</strong></p>");
            var sut = new ContentSavingHandler(
                contentTypeService.Object,
                securityAccessor.Object,
                Microsoft.Extensions.Options.Options.Create(new ArticulateOptions { AutoGenerateExcerpt = true }),
                markdownConverter.Object,
                richTextRenderer.Object);

            sut.Handle(new ContentSavingNotification([content.Object], new EventMessages()));

            var excerptValue = setValueCalls.LastOrDefault(x => x.Alias == "excerpt").Value?.ToString() ?? string.Empty;

            Assert.That(excerptValue, Does.Contain("Hello"));
            Assert.That(excerptValue, Does.Not.Contain("{\"markup\""));
        }
    }
}
