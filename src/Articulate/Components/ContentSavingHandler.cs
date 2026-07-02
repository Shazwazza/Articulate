#nullable enable
using Articulate.Options;
using Articulate.Services;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Articulate.Components
{
    /// <summary>
    /// Notification handler to set default values and auto-generate excerpts when Articulate content is saving.
    /// </summary>
    public sealed class ContentSavingHandler(
        IContentTypeService contentTypeService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IOptions<ArticulateOptions> articulateOptions,
        IArticulateMarkdownConverter articulateMarkdownConverter,
        IArticulateRichTextRenderer richTextRenderer)
        : INotificationHandler<ContentSavingNotification>
    {
        private readonly ArticulateOptions _articulateOptions = articulateOptions.Value;

        /// <inheritdoc/>
        public void Handle(ContentSavingNotification notification)
        {
            var saved = notification.SavedEntities.ToList();
            if (saved.Count == 0)
            {
                return;
            }

            var contentTypes = contentTypeService.GetMany(saved.Select(x => x.ContentTypeId).ToArray())
                .ToDictionary(x => x.Id);

            foreach (IContent content in saved)
            {
                IContentType contentType = contentTypes[content.ContentTypeId];

                if (IsArticulatePost(content))
                {
                    SetPostDefaults(content, contentType);

                    if (_articulateOptions.AutoGenerateExcerpt)
                    {
                        GenerateExcerptIfNeeded(content, contentType);
                    }
                }
            }
        }

        private static bool IsArticulatePost(IContent content) =>
            content.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.ArticulateRichText) ||
            content.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.ArticulateMarkdown);

        private void SetPostDefaults(IContent content, IContentType contentType)
        {
            // Set publishedDate if not already set
            content.SetAllPropertyCultureValues(
                "publishedDate",
                contentType,
                (c, _, culture) =>
                    c.GetValue("publishedDate", culture?.Culture) is null ? (DateTime?)DateTime.Now : null);

            // Set author if not already set
            content.SetAllPropertyCultureValues(
                "author",
                contentType,
                (c, _, culture) => c.GetValue("author", culture?.Culture) is null
                    ? backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Name
                    : null);

            // Keep comments enabled by default for new posts created outside the backoffice UI.
            if (!content.HasIdentity)
            {
                content.SetAllPropertyCultureValues(
                    "enableComments",
                    contentType,
                    (_, _, _) => 1);
            }
        }

        private void GenerateExcerptIfNeeded(IContent content, IContentType contentType)
        {
            // Generate excerpt from richText or markdown if not already set
            content.SetAllPropertyCultureValues(
                "excerpt",
                contentType,
                (c, ct, culture) =>
                {
                    var currentExcerpt = c.GetValue("excerpt", culture?.Culture)?.ToString();
                    if (!currentExcerpt.IsNullOrWhiteSpace())
                    {
                        return null;
                    }

                    return GenerateExcerptFromContent(c, ct, culture);
                });

            // Set socialDescription from excerpt if not already set
            if (content.HasProperty("socialDescription"))
            {
                content.SetAllPropertyCultureValues(
                    "socialDescription",
                    contentType,
                    (c, ct, culture) =>
                    {
                        var currentSocialDescription = c.GetValue("socialDescription", culture?.Culture)?.ToString();
                        if (!currentSocialDescription.IsNullOrWhiteSpace())
                        {
                            return null;
                        }

                        IPropertyType excerptProperty = ct.CompositionPropertyTypes.First(x => x.Alias == "excerpt");
                        return c.GetValue<string>(
                            "excerpt",
                            excerptProperty.VariesByCulture() ? culture?.Culture : null);
                    });
            }
        }

        private string GenerateExcerptFromContent(
            IContentBase content,
            IContentTypeComposition contentType,
            ContentCultureInfos? culture)
        {
            if (content.HasProperty("richText"))
            {
                IPropertyType richTextProperty = contentType.CompositionPropertyTypes.First(x => x.Alias == "richText");
                var val = content.GetValue<string>(
                    "richText",
                    richTextProperty.VariesByCulture() ? culture?.Culture : null);
                var markup = richTextRenderer.GetMarkup(val);
                return string.IsNullOrWhiteSpace(markup)
                    ? string.Empty
                    : _articulateOptions.GenerateExcerpt(markup);
            }

            if (content.HasProperty("markdown"))
            {
                IPropertyType markdownProperty = contentType.CompositionPropertyTypes.First(x => x.Alias == "markdown");
                var val = content.GetValue<string>(
                    "markdown",
                    markdownProperty.VariesByCulture() ? culture?.Culture : null);
                if (string.IsNullOrWhiteSpace(val))
                {
                    return string.Empty;
                }

                var html = articulateMarkdownConverter.ToHtml(val);
                return _articulateOptions.GenerateExcerpt(html);
            }

            return string.Empty;
        }
    }
}
