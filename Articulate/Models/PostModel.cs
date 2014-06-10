using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using CookComputing.XmlRpc;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class PostModel : MasterModel
    {
        private AuthorModel _author;

        public PostModel(IPublishedContent content)
            : base(content)
        {
        }

        public IEnumerable<string> Tags
        {
            get
            {
                var tags = this.GetPropertyValue<string>("tags");
                return tags.IsNullOrWhiteSpace() ? Enumerable.Empty<string>() : tags.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public IEnumerable<string> Categories
        {
            get
            {
                var tags = this.GetPropertyValue<string>("categories");
                return tags.IsNullOrWhiteSpace() ? Enumerable.Empty<string>() : tags.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public bool EnableComments
        {
            get { return Content.GetPropertyValue<bool>("enableComments", true); }
        }

        public AuthorModel Author
        {
            get
            {
                if (_author != null)
                {
                    return _author;
                }

                _author = new AuthorModel
                {
                    Name = Content.GetPropertyValue<string>("author", true)
                };

                //look up assocated author node if we can
                if (RootBlogNode != null)
                {
                    var authors = RootBlogNode.Children(content => content.DocumentTypeAlias.InvariantEquals("ArticulateAuthors")).FirstOrDefault();
                    if (authors != null)
                    {
                        var authorNode = authors.Children(content => content.Name.InvariantEquals(_author.Name)).FirstOrDefault();
                        if (authorNode != null)
                        {
                            _author.Bio = authorNode.GetPropertyValue<string>("authorBio");
                            _author.Url = authorNode.GetPropertyValue<string>("authorUrl");

                            _author.Image = authorNode.GetCropUrl(propertyAlias: "authorImage", imageCropMode: ImageCropMode.Max);
                        }
                    }
                }

                return _author;
            }
        }

        public string Excerpt
        {
            get { return this.GetPropertyValue<string>("excerpt"); }
        }

        public DateTime PublishedDate
        {
            get { return Content.GetPropertyValue<DateTime>("publishedDate"); }
        }

        public IHtmlString Body
        {
            get
            {
                if (this.HasProperty("richText"))
                {
                    return this.GetPropertyValue<IHtmlString>("richText");                    
                }
                else
                {
                    var val = this.GetPropertyValue<string>("markdown");
                    var md = new MarkdownDeep.Markdown();
                    return new MvcHtmlString(md.Transform(val));                    
                }
                
            }
        }
    }

}
