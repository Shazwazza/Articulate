﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Articulate.Options;
using CookComputing.XmlRpc;
using HeyRed.MarkdownSharp;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors.ValueConverters;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class PostModel : MasterModel, IImageModel
    {
        private PostAuthorModel _author;

        public PostModel(IPublishedContent content)
            : base(content)
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
                var authors = RootBlogNode?.Children(content => content.ContentType.Alias.InvariantEquals("ArticulateAuthors")).FirstOrDefault();
                var authorNode = authors?.Children(content => content.Name.InvariantEquals(_author.Name)).FirstOrDefault();
                if (authorNode != null)
                {
                    _author.Bio = authorNode.Value<string>("authorBio");
                    _author.Url = authorNode.Value<string>("authorUrl");
                    _author.Image = authorNode.Value<ImageCropperValue>("authorImage");
                    _author.BlogUrl = authorNode.Url;
                }

                return _author;
            }
        }

        public string Excerpt => this.Value<string>("excerpt");

        public DateTime PublishedDate => base.Unwrap().Value<DateTime>("publishedDate");

        private ImageCropperValue _postImage;

        private IPublishedContent _articulateImage;

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

        public IPublishedContent ArticulateImage
        {
            get
            {
                return _articulateImage ??
                       (_articulateImage = this.Value<IPublishedContent>("articulateImage"));
            }
        }


        private string _croppedPostImageUrl;

        private string _croppedArticulateImageUrl;

        /// <summary>
        /// Cropped version of the PostImageUrl
        /// </summary>
        public string CroppedPostImageUrl => _croppedPostImageUrl ?? (_croppedPostImageUrl =
                                                 PostImage != null
                                                     ? PostImage.Src + PostImage.GetCropUrl("wide") + "&upscale=false"
                                                     : null);

        public string CroppedArticulateImageUrl => _croppedArticulateImageUrl ?? (_croppedArticulateImageUrl =
                                                ArticulateImage != null
                                                     ? ArticulateImage.GetCropUrl("wide") + "&upscale=false"
                                                     : null);

        /// <summary>
        /// Social Meta Description
        /// </summary>
        public string SocialMetaDescription => this.Value<string>("socialDescription");

        public IHtmlString Body
        {
            get
            {
                if (this.HasProperty("richText"))
                {
                    return this.Value<IHtmlString>("richText");
                }
                else
                {
                    var val = this.Value<IHtmlString>("markdown");
                    return val;
                }

            }
        }

        public string ExternalUrl => this.Value<string>("externalUrl");

        ImageCropperValue IImageModel.Image => PostImage;
        string IImageModel.Name => Name;
        string IImageModel.Url => Url;
    }

}
