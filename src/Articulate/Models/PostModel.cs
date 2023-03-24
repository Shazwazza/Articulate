using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Html;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace Articulate.Models
{
    public class PostModel : MasterModel, IImageModel
    {
        private PostAuthorModel _author;

        public PostModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback, IVariationContextAccessor variationContextAccessor)
            : base(content, publishedValueFallback, variationContextAccessor)
        {
            PageTitle = Name + " - " + BlogTitle;
            PageDescription = Excerpt;
            PageTags = string.Join(",", Tags);
        }

        public IEnumerable<string> Tags
        {
            get
            {
                var tags = this.Value<IEnumerable<string>>("tags");
                return tags ?? Enumerable.Empty<string>();
            }
        }

        public IEnumerable<string> Categories
        {
            get
            {
                var tags = this.Value<IEnumerable<string>>("categories");
                return tags ?? Enumerable.Empty<string>();
            }
        }

        public bool EnableComments => base.Unwrap().Value<bool>("enableComments", fallback: Fallback.ToAncestors);

        public PostAuthorModel Author
        {
            get
            {
                if (_author != null)
                {
                    return _author;
                }

                _author = new PostAuthorModel
                {
                    Name = base.Unwrap().Value<string>("author", fallback: Fallback.ToAncestors)
                };

                //look up assocated author node if we can
                var authors = RootBlogNode?.Children(content => content.ContentType.Alias.InvariantEquals(ArticulateConstants.ArticulateAuthorsContentTypeAlias)).FirstOrDefault();
                var authorNode = authors?.Children(content => content.Name.InvariantEquals(_author.Name)).FirstOrDefault();
                if (authorNode != null)
                {
                    _author.Bio = authorNode.Value<string>("authorBio");
                    _author.Url = authorNode.Value<string>("authorUrl");
                    _author.Image = authorNode.Value<MediaWithCrops>("authorImage");
                    _author.BlogUrl = authorNode.Url();
                }

                return _author;
            }
        }

        public string Excerpt => this.Value<string>("excerpt");

        public DateTime PublishedDate => base.Unwrap().Value<DateTime>("publishedDate");

        private MediaWithCrops _postImage;

        /// <summary>
        /// Some blog post may have an associated image
        /// </summary>
        public MediaWithCrops PostImage => _postImage ??= base.Unwrap().Value<MediaWithCrops>("postImage");

        private string _croppedPostImageUrl;
        
        /// <summary>
        /// Cropped version of the PostImageUrl
        /// </summary>
        public string CroppedPostImageUrl
        {
            get
            {
                if (_croppedPostImageUrl != null)
                {
                    return _croppedPostImageUrl;
                }

                if (PostImage == null)
                {
                    return null;
                }

                var wideCropUrl = PostImage.GetCropUrl("wide");
                _croppedPostImageUrl = (wideCropUrl ?? string.Empty) + ((wideCropUrl != null && wideCropUrl.Contains('?')) ? "&" : "?");
                return _croppedPostImageUrl;
            }
        }

        /// <summary>
        /// Social Meta Description
        /// </summary>
        public string SocialMetaDescription => this.Value<string>("socialDescription");

        public IHtmlContent Body
        {
            get
            {
                return new HtmlString(
                    this.Value<IHtmlEncodedString>(
                        this.HasProperty("richText") ? "richText" : "markdown")
                    .ToHtmlString());

            }
        }

        public string ExternalUrl => this.Value<string>("externalUrl");

        MediaWithCrops IImageModel.Image => PostImage;

        string IImageModel.Name => Name;
        string IImageModel.Url => this.Url();
    }

}
