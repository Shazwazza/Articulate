#nullable enable
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;

namespace Articulate.Services
{
    public class ArticulateRichTextRenderer(
        IUmbracoContextAccessor umbracoContextAccessor,
        IJsonSerializer jsonSerializer,
        ILogger<ArticulateRichTextRenderer> logger)
        : IArticulateRichTextRenderer
    {
        public string GetMarkup(string? value)
        {
            return RichTextPropertyEditorHelper.TryParseRichTextEditorValue(value, jsonSerializer, logger, out RichTextEditorValue? richTextEditorValue)
                ? richTextEditorValue.Markup
                : value ?? string.Empty;
        }

        public string GetRenderedMarkup(IContent content, string propertyAlias = "richText")
        {
            if (umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? umbracoContext))
            {
                IPublishedContent? publishedContent = umbracoContext.Content?.GetById(content.Id);
                IHtmlEncodedString? rendered = publishedContent?.Value<IHtmlEncodedString>(propertyAlias);

                if (rendered?.ToHtmlString() is { } html)
                {
                    return html;
                }
            }

            return GetMarkup(content.GetValue<string>(propertyAlias));
        }
    }
}
