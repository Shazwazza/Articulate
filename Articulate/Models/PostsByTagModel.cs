using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Umbraco.Core;

namespace Articulate.Models
{
    public class PostsByTagModel
    {
        public PostsByTagModel(IEnumerable<PostModel> posts, string tagName, string tagUrl)
        {
            if (posts == null) throw new ArgumentNullException("posts");
            if (tagName == null) throw new ArgumentNullException("tagName");
            if (tagUrl == null) throw new ArgumentNullException("tagUrl");

            //resolve to array so it doesn't double lookup
            Posts = posts.ToArray();
            TagName = tagName;

            var safeEncoded = tagUrl.SafeEncodeUrlSegments();

            TagUrl = safeEncoded.Contains("//") ? safeEncoded : safeEncoded.EnsureStartsWith('/');
        }

        public IEnumerable<PostModel> Posts { get; private set; }
        public string TagName { get; private set; }
        public string TagUrl { get; private set; }

        private int? _count;
        public int PostCount
        {
            get
            {
                if (_count.HasValue == false)
                {
                    _count = Posts.Count();
                }
                return _count.Value;
            }
        }
    }

}