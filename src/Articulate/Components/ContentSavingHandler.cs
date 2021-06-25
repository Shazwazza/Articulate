using Articulate.Options;
using System;
using System.Linq;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Articulate.Components
{

    public sealed class ContentSavingHandler : INotificationHandler<ContentSavingNotification>
    {
        private readonly IContentTypeService _contentTypeService;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        public ContentSavingHandler(IContentTypeService contentTypeService, IUmbracoContextAccessor umbracoContextAccessor)
        {
            _contentTypeService = contentTypeService;
            _umbracoContextAccessor = umbracoContextAccessor;
        }

        public void Handle(ContentSavingNotification notification)
        {
            var saved = notification.SavedEntities.ToList();
            if (saved.Count == 0) return;

            var contentTypes = _contentTypeService.GetAll(saved.Select(x => x.ContentTypeId).ToArray()).ToDictionary(x => x.Id);

            foreach (var content in saved)
            {
                if (content.ContentType.Alias.InvariantEquals("ArticulateRichText")
                    || content.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                {
                    content.SetAllPropertyCultureValues(
                        "publishedDate",
                        contentTypes[content.ContentTypeId],
                        // if the publishedDate is not already set, then set it 
                        (c, ct, culture) => c.GetValue("publishedDate", culture?.Culture) == null ? (DateTime?)DateTime.Now : null);

                    content.SetAllPropertyCultureValues(
                        "author",
                        contentTypes[content.ContentTypeId],
                        // if the author is not already set, then set it 
                        (c, ct, culture) => c.GetValue("author", culture?.Culture) == null ? _umbracoContextAccessor?.UmbracoContext?.Security?.CurrentUser?.Name : null);

                    if (!content.HasIdentity)
                    {
                        // default values
                        content.SetAllPropertyCultureValues(
                            "enableComments",
                            contentTypes[content.ContentTypeId],
                            (c, ct, culture) => 1);
                    }
                }

                if (_configs.Articulate().AutoGenerateExcerpt)
                {
                    if (content.ContentType.Alias.InvariantEquals("ArticulateRichText")
                        || content.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                    {

                        // fill in the excerpt if it is empty
                        content.SetAllPropertyCultureValues(
                            "excerpt",
                            contentTypes[content.ContentTypeId],
                            (c, ct, culture) =>
                            {
                                // don't set it if it's already set
                                var currentExcerpt = c.GetValue("excerpt", culture?.Culture)?.ToString();
                                if (!currentExcerpt.IsNullOrWhiteSpace()) return null;

                                if (content.HasProperty("richText"))
                                {
                                    var richTextProperty = ct.CompositionPropertyTypes.First(x => x.Alias == "richText");
                                    var val = c.GetValue<string>("richText", richTextProperty.VariesByCulture() ? culture?.Culture : null);
                                    return _configs.Articulate().GenerateExcerpt(val);
                                }
                                else
                                {
                                    var markdownProperty = ct.CompositionPropertyTypes.First(x => x.Alias == "markdown");
                                    var val = c.GetValue<string>("markdown", markdownProperty.VariesByCulture() ? culture?.Culture : null);
                                    var md = new Markdown();
                                    var html = md.Transform(val);
                                    return _configs.Articulate().GenerateExcerpt(html);
                                }
                            });

                        //now fill in the social description if it is empty with the excerpt
                        if (content.HasProperty("socialDescription"))
                        {
                            content.SetAllPropertyCultureValues(
                                "socialDescription",
                                contentTypes[content.ContentTypeId],
                                (c, ct, culture) =>
                                {
                                    // don't set it if it's already set
                                    var currentSocialDescription = c.GetValue("socialDescription", culture?.Culture)?.ToString();
                                    if (!currentSocialDescription.IsNullOrWhiteSpace()) return null;

                                    var excerptProperty = ct.CompositionPropertyTypes.First(x => x.Alias == "excerpt");
                                    return content.GetValue<string>("excerpt", excerptProperty.VariesByCulture() ? culture?.Culture : null);
                                });
                        }
                    }
                }
            }
        }
    }
}
