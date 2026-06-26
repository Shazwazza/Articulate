#nullable enable
using Microsoft.AspNetCore.Html;
using Articulate.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Strings;

namespace Articulate.Models
{
    public sealed class PostModel : MasterModel, IImageModel
    {
        public PostModel(
            IPublishedContent content,
            IPublishedValueFallback publishedValueFallback,
            ArticulateCommentsOptions? commentsOptions = null)
            : base(content, publishedValueFallback, commentsOptions)
        {
            PageTitle = Name + " - " + BlogTitle;
            PageDescription = Excerpt;
            PageTags = string.Join(",", Tags);
        }

        /// <summary>
        /// Gets the tags associated with the post.
        /// </summary>
        public IEnumerable<string> Tags => this.Value<IEnumerable<string>>("tags") ?? [];

        /// <summary>
        /// Gets the categories associated with the post.
        /// </summary>
        public IEnumerable<string> Categories => this.Value<IEnumerable<string>>("categories") ?? [];

        /// <summary>
        /// Gets a value indicating whether comments are enabled for this post.
        /// </summary>
        public bool EnableComments => Unwrap().Value<bool>("enableComments", fallback: Fallback.ToAncestors);

        /// <summary>
        /// Gets the author of the post.
        /// </summary>
        public PostAuthorModel Author
        {
            get
            {
                if (field is not null)
                {
                    return field;
                }

                field = new PostAuthorModel
                {
                    Name = Unwrap().Value<string>("author", fallback: Fallback.ToAncestors),
                };

                // look up associated author node if we can
                IEnumerable<IPublishedContent> authorContainers =
                    RootBlogNode.Children().Where(content =>
                        content.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.ArticulateAuthors));
                IPublishedContent? authors = authorContainers.FirstOrDefault();

                IEnumerable<IPublishedContent> authorNodes =
                    authors?.Children(content => content.Name.InvariantEquals(field.Name))
                    ?? [];
                IPublishedContent? authorNode = authorNodes.FirstOrDefault();

                if (authorNode is null)
                {
                    return field;
                }

                field.Bio = authorNode.Value<string>("authorBio");
                field.Url = authorNode.Value<string>("authorUrl").ToSafeHrefUrl();
                field.Image = authorNode.Value<MediaWithCrops>("authorImage");
                field.BlogUrl = authorNode.Url().ToSafeHrefUrl();

                return field;
            }
        }

        /// <summary>
        /// Gets the post excerpt.
        /// </summary>
        public string Excerpt => this.Value<string>("excerpt") ?? string.Empty;

        /// <summary>
        /// Gets the published date of the post.
        /// </summary>
        public DateTime PublishedDate => Unwrap().Value<DateTime>("publishedDate");

        /// <summary>
        /// Gets the post image item.
        /// </summary>
        public MediaWithCrops? PostImage => field ??= Unwrap().Value<MediaWithCrops>("postImage");

        /// <summary>
        /// Gets the wide cropped image URL for the post.
        /// </summary>
        public string CroppedPostImageUrl
        {
            get
            {
                if (field is not null)
                {
                    return field;
                }

                if (PostImage is null)
                {
                    return string.Empty;
                }

                field = PostImage.GetCropUrl(cropAlias: "wide", preferFocalPoint: true, useCropDimensions: true) ??
                        string.Empty;
                return field;
            }
        }

        /// <summary>
        /// Gets the social meta description.
        /// </summary>
        public string SocialMetaDescription => this.Value<string>("socialDescription") ?? string.Empty;

        /// <summary>
        /// Gets the post body content as HTML.
        /// </summary>
        public IHtmlContent Body =>
            new HtmlString(
                this.Value<IHtmlEncodedString>(
                        this.HasProperty("richText") ? "richText" : "markdown")
                    ?.ToHtmlString());

        /// <summary>
        /// Gets the external URL for the post if set.
        /// </summary>
        // Not used internally or by default themes, but exposed for custom themes
        public string ExternalUrl => this.Value<string>("externalUrl") ?? string.Empty;

        /// <inheritdoc/>
        MediaWithCrops? IImageModel.Image => PostImage;

        /// <inheritdoc/>
        string IImageModel.Name => Name;

        /// <inheritdoc/>
        string IImageModel.Url => this.Url();

        /// <inheritdoc/>
        string IImageModel.CroppedWideUrl => CroppedPostImageUrl;
    }
}
