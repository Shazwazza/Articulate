#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Articulate.Options;
using Articulate.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Serialization;

namespace Articulate.Tests.Services
{
    [TestFixture]
    public class ArticulateRichTextRendererTests
    {
        private static readonly IArticulateRichTextRenderer Renderer = new ArticulateRichTextRenderer(
            new EmptyUmbracoContextAccessor(),
            new TestJsonSerializer(),
            NullLogger<ArticulateRichTextRenderer>.Instance);

        [Test]
        public void GetMarkup_returns_raw_html_unchanged()
        {
            const string input = "<p>Hello <strong>world</strong></p>";

            Assert.That(Renderer.GetMarkup(input), Is.EqualTo(input));
        }

        [Test]
        public void GetMarkup_returns_markup_from_rte_json()
        {
            const string input = """{"markup":"<p>Hello</p>"}""";

            Assert.That(Renderer.GetMarkup(input), Is.EqualTo("<p>Hello</p>"));
        }

        [Test]
        public void GetMarkup_returns_markup_without_rendering_rich_text_blocks()
        {
            const string input =
                """
                {
                  "markup": "<p>Hello</p><umb-rte-block data-content-key=\"11111111-1111-1111-1111-111111111111\"></umb-rte-block>",
                  "blocks": {
                    "layout": {
                      "Umbraco.RichText": [
                        { "contentKey": "11111111-1111-1111-1111-111111111111" }
                      ]
                    },
                    "contentData": [
                      {
                        "contentTypeKey": "22222222-2222-2222-2222-222222222222",
                        "key": "11111111-1111-1111-1111-111111111111",
                        "values": [
                          { "editorAlias": "Umbraco.TextBox", "alias": "title", "value": "Block title" },
                          { "editorAlias": "Umbraco.TextArea", "alias": "body", "value": "Block body" }
                        ]
                      }
                    ],
                    "settingsData": [],
                    "expose": []
                  }
                }
                """;

            string markup = Renderer.GetMarkup(input);

            Assert.That(markup, Is.EqualTo("<p>Hello</p><umb-rte-block data-content-key=\"11111111-1111-1111-1111-111111111111\"></umb-rte-block>"));
            Assert.That(markup, Does.Not.Contain("Block title"));
            Assert.That(markup, Does.Not.Contain("Block body"));
        }

        [Test]
        public void GetMarkup_returns_empty_string_for_blank_values()
        {
            Assert.That(Renderer.GetMarkup(string.Empty), Is.Empty);
            Assert.That(Renderer.GetMarkup(null), Is.Empty);
            Assert.That(Renderer.GetMarkup("   "), Is.EqualTo("   "));
        }

        [Test]
        public void GetMarkup_returns_original_value_for_invalid_json()
        {
            const string input = """{"markup":""";

            Assert.That(Renderer.GetMarkup(input), Is.EqualTo(input));
        }

        [Test]
        public void GetMarkup_returns_original_value_for_json_without_markup()
        {
            const string input = """{"title":"No markup"}""";

            Assert.That(Renderer.GetMarkup(input), Is.EqualTo(input));
        }

        [Test]
        public void GetMarkup_supports_excerpt_generation_from_rich_text_json()
        {
            var options = new ArticulateOptions();
            const string input = """{"markup":"<p>Hello <strong>world</strong></p>","blocks":null}""";

            string excerpt = options.GenerateExcerpt(Renderer.GetMarkup(input));

            Assert.That(excerpt, Does.Contain("Hello"));
            Assert.That(excerpt, Does.Not.Contain("{\"markup\""));
        }

        private sealed class EmptyUmbracoContextAccessor : Umbraco.Cms.Core.Web.IUmbracoContextAccessor
        {
            public bool TryGetUmbracoContext([NotNullWhen(true)] out Umbraco.Cms.Core.Web.IUmbracoContext? umbracoContext)
            {
                umbracoContext = null;
                return false;
            }

            public void Clear() { }
            public void Set(Umbraco.Cms.Core.Web.IUmbracoContext umbracoContext) { }
        }

        private sealed class TestJsonSerializer : IJsonSerializer
        {
            private static readonly JsonSerializerOptions Options = new()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            public string Serialize(object? input) => JsonSerializer.Serialize(input, Options);
            public T? Deserialize<T>(string input)
            {
                if (typeof(T) == typeof(RichTextEditorValue))
                {
                    return DeserializeRichTextEditorValue<T>(input);
                }

                return JsonSerializer.Deserialize<T>(input, Options);
            }

            public bool TryDeserialize<T>(object input, [NotNullWhen(true)] out T? value)
                where T : class
            {
                try
                {
                    value = input is string raw
                        ? Deserialize<T>(raw)
                        : Deserialize<T>(JsonSerializer.Serialize(input, Options));
                    return value is not null;
                }
                catch (JsonException)
                {
                    value = null;
                    return false;
                }
            }

            private static T? DeserializeRichTextEditorValue<T>(string input)
            {
                using var document = JsonDocument.Parse(input);
                JsonElement root = document.RootElement;

                if (root.TryGetProperty("markup", out JsonElement markupElement) is false ||
                    markupElement.ValueKind != JsonValueKind.String)
                {
                    return default;
                }

                RichTextBlockValue? blocks = root.TryGetProperty("blocks", out JsonElement blocksElement) &&
                                             blocksElement.ValueKind != JsonValueKind.Null
                    ? new RichTextBlockValue([new RichTextBlockLayoutItem(Guid.Parse("11111111-1111-1111-1111-111111111111"))])
                    : null;

                return (T)(object)new RichTextEditorValue
                {
                    Markup = markupElement.GetString() ?? string.Empty,
                    Blocks = blocks
                };
            }
        }
    }
}
