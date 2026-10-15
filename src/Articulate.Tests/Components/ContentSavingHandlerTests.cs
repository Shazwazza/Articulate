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
            var sut = new ContentSavingHandler(
                contentTypeService.Object,
                securityAccessor.Object,
                Microsoft.Extensions.Options.Options.Create(new ArticulateOptions { AutoGenerateExcerpt = false }),
                markdownConverter.Object);

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
            var sut = new ContentSavingHandler(
                contentTypeService.Object,
                securityAccessor.Object,
                Microsoft.Extensions.Options.Options.Create(new ArticulateOptions { AutoGenerateExcerpt = false }),
                markdownConverter.Object);

            sut.Handle(new ContentSavingNotification([content.Object], new EventMessages()));

            Assert.That(
                setValueCalls,
                Does.Not.Contain("enableComments"),
                "Expected enableComments not to be set for existing content.");
        }

        [Test]
        public void Handle_generates_excerpt_and_social_description_from_markdown_when_enabled()
        {
            const string markdown = "# A markdown post";
            const string html = "<h1>A markdown post</h1>";
            const string generatedExcerpt = "A generated excerpt";

            Mock<IPropertyType> excerptProperty = new();
            excerptProperty.SetupGet(x => x.Alias).Returns("excerpt");

            Mock<IPropertyType> socialDescriptionProperty = new();
            socialDescriptionProperty.SetupGet(x => x.Alias).Returns("socialDescription");

            Mock<IPropertyType> markdownProperty = new();
            markdownProperty.SetupGet(x => x.Alias).Returns("markdown");

            Mock<IContentType> contentType = new();
            contentType.SetupGet(x => x.Id).Returns(101);
            contentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateMarkdown);
            contentType.SetupGet(x => x.CompositionPropertyTypes).Returns(
                [excerptProperty.Object, socialDescriptionProperty.Object, markdownProperty.Object]);

            Mock<ISimpleContentType> simpleContentType = new();
            simpleContentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateMarkdown);

            Mock<IContent> content = new();
            content.SetupGet(x => x.ContentTypeId).Returns(101);
            content.SetupGet(x => x.ContentType).Returns(simpleContentType.Object);
            content.SetupGet(x => x.HasIdentity).Returns(true);
            content.Setup(x => x.HasProperty("markdown")).Returns(true);
            content.Setup(x => x.HasProperty("socialDescription")).Returns(true);
            content.Setup(x => x.GetValue("publishedDate", null, null, false)).Returns(DateTime.UtcNow);
            content.Setup(x => x.GetValue("author", null, null, false)).Returns("author");
            content.Setup(x => x.GetValue("excerpt", null, null, false)).Returns((object?)null);
            content.Setup(x => x.GetValue<string>("excerpt", null, null, false)).Returns(generatedExcerpt);
            content.Setup(x => x.GetValue("socialDescription", null, null, false)).Returns((object?)null);
            content.Setup(x => x.GetValue<string>("markdown", null, null, false)).Returns(markdown);

            var setValueCalls = new List<(string Alias, object? Value)>();
            content
                .Setup(x => x.SetValue(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Callback<string, object?, string?, string?>((alias, value, _, _) => setValueCalls.Add((alias, value)));

            string? convertedMarkdown = null;
            string? excerptSource = null;
            Mock<IArticulateMarkdownConverter> markdownConverter = new();
            markdownConverter
                .Setup(x => x.ToHtml(It.IsAny<string>()))
                .Returns((string value) =>
                {
                    convertedMarkdown = value;
                    return html;
                });

            Mock<IContentTypeService> contentTypeService = new();
            contentTypeService
                .Setup(x => x.GetMany(It.Is<int[]>(ids => ids.Length == 1 && ids[0] == 101)))
                .Returns([contentType.Object]);

            Mock<IBackOfficeSecurityAccessor> securityAccessor = new();
            var sut = new ContentSavingHandler(
                contentTypeService.Object,
                securityAccessor.Object,
                Microsoft.Extensions.Options.Options.Create(new ArticulateOptions
                {
                    AutoGenerateExcerpt = true,
                    GenerateExcerpt = value =>
                    {
                        excerptSource = value;
                        return generatedExcerpt;
                    }
                }),
                markdownConverter.Object);

            sut.Handle(new ContentSavingNotification([content.Object], new EventMessages()));

            Assert.That(convertedMarkdown, Is.EqualTo(markdown));
            Assert.That(excerptSource, Is.EqualTo(html));
            Assert.That(setValueCalls, Has.Count.EqualTo(2));
            Assert.That(setValueCalls.Single(x => x.Alias == "excerpt").Value, Is.EqualTo(generatedExcerpt));
            Assert.That(setValueCalls.Single(x => x.Alias == "socialDescription").Value, Is.EqualTo(generatedExcerpt));
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
            var sut = new ContentSavingHandler(
                contentTypeService.Object,
                securityAccessor.Object,
                Microsoft.Extensions.Options.Options.Create(new ArticulateOptions { AutoGenerateExcerpt = false }),
                markdownConverter.Object);

            sut.Handle(new ContentSavingNotification([content.Object], new EventMessages()));

            Assert.That(setValueCalls, Is.Empty, "Expected no SetValue calls for non-Articulate content.");
        }
    }
}
