#nullable enable
using Articulate.PropertyEditors;
using Articulate.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Templates;
using UmbracoMarkdownConverter = Umbraco.Cms.Core.Strings.IMarkdownToHtmlConverter;

namespace Articulate.Tests.PropertyEditors
{
    [TestFixture]
    public class ArticulateMarkdownEditorValueConverterTests
    {
        private const string ArticulateAlias = "Articulate.MarkdownEditor";
        private const string UmbracoAlias = "Umbraco.MarkdownEditor";

        [Test]
        public void IsConverter_returns_true_when_EditorUiAlias_matches_Articulate_alias()
        {
            ArticulateMarkdownEditorValueConverter sut = CreateSut(out _);
            Mock<IPublishedPropertyType> propertyType = MockPropertyType(editorUiAlias: ArticulateAlias, editorAlias: UmbracoAlias);

            Assert.That(sut.IsConverter(propertyType.Object), Is.True);
        }

        [Test]
        public void IsConverter_returns_true_when_EditorAlias_matches_Articulate_alias()
        {
            ArticulateMarkdownEditorValueConverter sut = CreateSut(out _);
            Mock<IPublishedPropertyType> propertyType = MockPropertyType(editorUiAlias: "Other.Ui", editorAlias: ArticulateAlias);

            Assert.That(sut.IsConverter(propertyType.Object), Is.True);
        }

        [Test]
        public void IsConverter_returns_false_for_native_Umbraco_Markdown_alias()
        {
            ArticulateMarkdownEditorValueConverter sut = CreateSut(out _);
            Mock<IPublishedPropertyType> propertyType = MockPropertyType(editorUiAlias: UmbracoAlias, editorAlias: UmbracoAlias);

            Assert.That(sut.IsConverter(propertyType.Object), Is.False);
        }

        [Test]
        public void ConvertIntermediateToObject_delegates_non_null_markdown_to_articulate_converter()
        {
            ArticulateMarkdownEditorValueConverter sut = CreateSut(out Mock<IArticulateMarkdownConverter> articulateConverter);
            articulateConverter.Setup(x => x.ToHtml("# hi")).Returns("<h1>hi</h1>");
            Mock<IPublishedPropertyType> propertyType = MockPropertyType(editorUiAlias: ArticulateAlias, editorAlias: ArticulateAlias);

            object result = sut.ConvertIntermediateToObject(
                Mock.Of<IPublishedElement>(), propertyType.Object, PropertyCacheLevel.Element, "# hi", preview: false);

            string html = AssertHtml(result);
            Assert.That(html, Is.EqualTo("<h1>hi</h1>"));
            articulateConverter.Verify(x => x.ToHtml("# hi"), Times.Once);
        }

        [Test]
        public void ConvertIntermediateToObject_returns_empty_for_null_intermediate()
        {
            ArticulateMarkdownEditorValueConverter sut = CreateSut(out Mock<IArticulateMarkdownConverter> articulateConverter);
            Mock<IPublishedPropertyType> propertyType = MockPropertyType(editorUiAlias: ArticulateAlias, editorAlias: ArticulateAlias);

            object result = sut.ConvertIntermediateToObject(
                Mock.Of<IPublishedElement>(), propertyType.Object, PropertyCacheLevel.Element, null, preview: false);

            string html = AssertHtml(result);
            Assert.That(html, Is.EqualTo(string.Empty));
            articulateConverter.Verify(x => x.ToHtml(It.IsAny<string>()), Times.Never);
        }

        // HtmlLocalLinkParser and HtmlUrlParser are sealed, so Moq can't mock them
        // directly; construct real instances backed by Moq'd dependency interfaces.
        // The Articulate override never invokes the parsers — they only have to exist
        // for the base ctor to assign.
        private static ArticulateMarkdownEditorValueConverter CreateSut(out Mock<IArticulateMarkdownConverter> articulateConverter)
        {
            articulateConverter = new Mock<IArticulateMarkdownConverter>(MockBehavior.Strict);
            return new ArticulateMarkdownEditorValueConverter(
                new HtmlLocalLinkParser(Mock.Of<IPublishedUrlProvider>()),
                new HtmlUrlParser(
                    Mock.Of<IOptionsMonitor<ContentSettings>>(),
                    Mock.Of<ILogger<HtmlUrlParser>>(),
                    Mock.Of<IProfilingLogger>(),
                    Mock.Of<IIOHelper>()),
                Mock.Of<UmbracoMarkdownConverter>(),
                articulateConverter.Object);
        }

        private static Mock<IPublishedPropertyType> MockPropertyType(string editorUiAlias, string editorAlias)
        {
            var propertyType = new Mock<IPublishedPropertyType>();
            propertyType.SetupGet(x => x.EditorUiAlias).Returns(editorUiAlias);
            propertyType.SetupGet(x => x.EditorAlias).Returns(editorAlias);
            return propertyType;
        }

        private static string AssertHtml(object result)
        {
            var html = (IHtmlEncodedString)(result as IHtmlEncodedString
                ?? throw new InvalidOperationException($"Expected HtmlEncodedString but got {result?.GetType().FullName ?? "null"}."));
            return html.ToString() ?? string.Empty;
        }
    }
}
