using System;
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
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class PostModel : MasterModel
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

        public bool EnableComments => Content.GetPropertyValue<bool>("enableComments", true);

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
                    Name = Content.GetPropertyValue<string>("author", true)
                };

                //look up assocated author node if we can
                var authors = RootBlogNode?.Children(content => content.ContentType.Alias.InvariantEquals("ArticulateAuthors")).FirstOrDefault();
                var authorNode = authors?.Children(content => content.Name.InvariantEquals(_author.Name)).FirstOrDefault();
                if (authorNode != null)
                {
                    _author.Bio = authorNode.Value<string>("authorBio");
                    _author.Url = authorNode.Value<string>("authorUrl");

                    var imageVal = authorNode.Value<string>("authorImage");
                    _author.Image = !imageVal.IsNullOrWhiteSpace()
                        ? authorNode.GetCropUrl(propertyAlias: "authorImage", imageCropMode: ImageCropMode.Max) 
                        : null;

                    _author.BlogUrl = authorNode.Url;
                }

                return _author;
            }
        }

        public string Excerpt => this.Value<string>("excerpt");

        public DateTime PublishedDate => Content.GetPropertyValue<DateTime>("publishedDate");

        /// <summary>
        /// Some blog post may have an associated image
        /// </summary>
        public string PostImageUrl => Content.GetPropertyValue<string>("postImage");

        /// <summary>
        /// Cropped version of the PostImageUrl
        /// </summary>
        public string CroppedPostImageUrl => !PostImageUrl.IsNullOrWhiteSpace() 
            ? this.GetCropUrl("postImage", "wide") 
            : null;

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
                    var val = this.Value<string>("markdown");
                    var md = new MarkdownDeep.Markdown();
                    Current.Configs.Articulate().MarkdownDeepOptionsCallBack(md);
                    return new MvcHtmlString(md.Transform(val));                    
                }
                
            }
        }

        public string ExternalUrl => this.Value<string>("externalUrl");
    }

}
