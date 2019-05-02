using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors.ValueConverters;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class AuthorModel : ListModel, IImageModel
    {        
        private DateTime? _lastPostDate;
        
        public AuthorModel(IPublishedContent content, IEnumerable<IPublishedContent> listItems, PagerModel pager, int postCount) 
            : base(content, listItems, pager)
        {
            PostCount = postCount;
        }        

        public string Bio => this.Value<string>("authorBio");

        public string AuthorUrl => this.Value<string>("authorUrl");

        private ImageCropperValue _image;
        public ImageCropperValue Image => (_image ?? (_image = base.Unwrap().Value<ImageCropperValue>("authorImage"))).Src.IsNullOrWhiteSpace() ? null : _image;
       
        public int PostCount { get; }

        //We know the list of posts passed in is already ordered descending so get the first
        public DateTime? LastPostDate => _lastPostDate ?? (_lastPostDate = Children.FirstOrDefault()?.Value<DateTime>("publishedDate"));
    }

}
