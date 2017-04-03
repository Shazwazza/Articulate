using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class AuthorModel : MasterModel
    {
        private readonly UmbracoHelper _umbracoHelper;

        /// <summary>
        /// Constructor specifying specific posts for the author
        /// </summary>
        /// <param name="content"></param>
        /// <param name="posts"></param>
        public AuthorModel(IPublishedContent content, IEnumerable<PostModel> posts) 
            : base(content)
        {
            _posts = posts;
        }

        /// <summary>
        /// Constructor that will force the lazy resolution of an author's posts
        /// </summary>
        /// <param name="content"></param>
        /// <param name="umbracoHelper"></param>
        public AuthorModel(IPublishedContent content, UmbracoHelper umbracoHelper)
            : base(content)
        {            
            if (umbracoHelper == null) throw new ArgumentNullException(nameof(umbracoHelper));
            _umbracoHelper = umbracoHelper;
        }

        public string Bio => this.GetPropertyValue<string>("authorBio");

        public string AuthorUrl => this.GetPropertyValue<string>("authorUrl");

        private string _image;
        public string Image
        {
            get
            {
                if (_image != null) return _image;

                var imageVal = this.GetPropertyValue<string>("authorImage");
                _image =  !imageVal.IsNullOrWhiteSpace()
                    ? this.GetCropUrl("authorImage", "wide")
                    : null;

                return _image;
            }
        }

        public IEnumerable<PostModel> Posts
        {
            get
            {
                if (_posts != null) return _posts;
                if (_umbracoHelper == null) return Enumerable.Empty<PostModel>();
                _posts = _umbracoHelper.GetContentByAuthor(this);
                return _posts;
            }
        }

        private int? _postCount;
        public int PostCount
        {
            get
            {
                if (_postCount.HasValue) return _postCount.Value;
                _postCount = Posts?.Count();
                return _postCount ?? 0;
            }
        }

        private DateTime? _lastPostDate;
        private IEnumerable<PostModel> _posts;

        public DateTime? LastPostDate
        {
            get { return _lastPostDate ?? (_lastPostDate = Posts?.OrderByDescending(c => c.PublishedDate).FirstOrDefault()?.PublishedDate); }
        }
    }

}
