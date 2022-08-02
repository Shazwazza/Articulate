using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Html;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace Articulate.Models
{
    public class PostModel : MasterModel, IImageModel
    {
        private PostAuthorModel _author;

        public PostModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback, IVariationContextAccessor variationContextAccessor, IImageUrlGenerator imageUrlGenerator)
            : base(content, publishedValueFallback, variationContextAccessor)
        {
            PageTitle = Name + " - " + BlogTitle;
            PageDescription = Excerpt;
            PageTags = string.Join(",", Tags);
            _imageUrlGenerator = imageUrlGenerator;
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
                    _author.Image = authorNode.Value<ImageCropperValue>("authorImage");
                    _author.BlogUrl = authorNode.Url();
                }

                return _author;
            }
        }

        public string Excerpt => this.Value<string>("excerpt");

        public DateTime PublishedDate => base.Unwrap().Value<DateTime>("publishedDate");

        private ImageCropperValue _postImage;

        /// <summary>
        /// Some blog post may have an associated image
        /// </summary>
        public ImageCropperValue PostImage
        {
            get
            {
                if (_postImage == null)
                    _postImage = base.Unwrap().Value<ImageCropperValue>("postImage");
                return _postImage == null || _postImage.Src.IsNullOrWhiteSpace() ? null : _postImage;
            }
        }

        private string _croppedPostImageUrl;
        private readonly IImageUrlGenerator _imageUrlGenerator;

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

                var wideCropUrl = PostImage.GetCropUrl("wide", _imageUrlGenerator);
                _croppedPostImageUrl = PostImage.Src + (wideCropUrl ?? string.Empty) + ((wideCropUrl != null && wideCropUrl.Contains('?')) ? "&" : "?");
                return _croppedPostImageUrl;
            }
        }

        /// <summary>
        /// Social Meta Description
        /// </summary>
        public string SocialMetaDescription => this.Value<string>("socialDescription");

        public IHtmlEncodedString Body
        {
            get
            {
                if (this.HasProperty("richText"))
                {
                    // Worth noting that newer Umbraco returns RTE as IHtmlEncodedString and not HtmlString
                    var val = this.Value<IHtmlEncodedString>("richText");
                    return val;
                }
                else
                {
                    var val = this.Value<IHtmlEncodedString>("markdown");
                    return val;
                }
                
            }
        }

        public string ExternalUrl => this.Value<string>("externalUrl");

        ImageCropperValue IImageModel.Image => PostImage;
        string IImageModel.Name => Name;
        string IImageModel.Url => this.Url();
    }

}
