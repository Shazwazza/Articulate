using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class AuthorModel : ListModel
    {        
        private DateTime? _lastPostDate;
        
        public AuthorModel(IPublishedContent content, IEnumerable<IPublishedContent> listItems, PagerModel pager, int postCount) 
            : base(content, listItems, pager)
        {
            PostCount = postCount;
        }        

        public string Bio => this.Value<string>("authorBio");

        public string AuthorUrl => this.Value<string>("authorUrl");

        private string _image;
        public string Image
        {
            get
            {
                if (_image != null) return _image;

                var imageVal = this.Value<string>("authorImage");
                _image =  !imageVal.IsNullOrWhiteSpace()
                    ? this.GetCropUrl("authorImage", "wide")
                    : null;

                return _image;
            }
        }
        
        public int PostCount { get; }


        public DateTime? LastPostDate
        {
            get
            {
                //We know the list of posts passed in is already ordered descending so get the first
                return _lastPostDate ?? (_lastPostDate = Children.FirstOrDefault()?.Value<DateTime>("publishedDate"));
            }
        }
    }

}
