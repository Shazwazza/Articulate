using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class PostModel : BlogModel
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
                        }
                    }
                }

                return _author;
            }
        }

        public string Excerpt
        {
            get
            {
                //TODO: Create a property for this, for now we'll just reduce
                if (this.HasProperty("RichText"))
                {
                    var val = this.GetPropertyValue<string>("richText");
                    return val.IsNullOrWhiteSpace() ? string.Empty : string.Join("", val.Take(200));
                }
                else
                {
                    var val = this.GetPropertyValue<string>("markDown");
                    return val.IsNullOrWhiteSpace() ? string.Empty : string.Join("", val.Take(200));
                }
                
            }
        }

        public DateTime PublishedDate
        {
            get { return Content.GetPropertyValue<DateTime>("publishedDate"); }
        }
    }
}
