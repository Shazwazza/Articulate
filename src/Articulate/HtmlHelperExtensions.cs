#nullable enable
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using PublishedContentExtensions = Articulate.Models.PublishedContentExtensions;

namespace Articulate
{
    public static class HtmlHelperExtensions
    {
        /// <summary>
        ///     Adds generic social meta tags.
        /// </summary>
        public static IHtmlContent SocialMetaTags(this IHtmlHelper html, IMasterModel model)
        {
            var builder = new HtmlContentBuilder();
            model.SocialMetaTags(builder);

            if (model is PostModel postModel)
            {
                PublishedContentExtensions.PostSocialMetaTags(postModel, html.ViewContext.HttpContext.Request, builder);
            }

            return builder;
        }
    }
}
