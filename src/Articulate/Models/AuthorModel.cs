using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    using System;
    using System.Linq;

    public class AuthorModel : MasterModel
    {
        public AuthorModel(IPublishedContent content)
            : base(content)
        {
        }

        public AuthorModel(IPublishedContent content, IEnumerable<PostModel> posts)
            : base(content)
        {
            Posts = posts;
        }

        public string Bio
        {
            get { return this.GetPropertyValue<string>("authorBio"); }
        }

        public string AuthorUrl
        {
            get { return this.GetPropertyValue<string>("authorUrl"); }
        }

        public string Image
        {
            get
            {
                var imageVal = this.GetPropertyValue<string>("authorImage");
                return !imageVal.IsNullOrWhiteSpace()
                    ? this.GetCropUrl(propertyAlias: "authorImage", imageCropMode: ImageCropMode.Max)
                    : null;
            }
        }

        public IEnumerable<PostModel> Posts { get; set; }

        public int PostCount
        {
            get
            {
                if (Posts != null)
                {
                    return Posts.Count();
                }

                return 0;
            }
        }

        public DateTime? LastPostDate
        {
            get
            {
                if (Posts != null)
                {
                    return Posts
                        .OrderByDescending(c => c.PublishedDate)
                        .First()
                        .PublishedDate;
                }

                return null;
            }
        }
    }

}
