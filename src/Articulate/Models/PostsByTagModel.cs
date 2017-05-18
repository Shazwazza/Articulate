using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;

namespace Articulate.Models
{
    public class PostsByTagModel
    {
        public PostsByTagModel(IEnumerable<PostModel> posts, string tagName, string tagUrl)
            : this(posts, tagName, tagUrl, -1)
        {
        }

        public PostsByTagModel(IEnumerable<PostModel> posts, string tagName, string tagUrl, int count)
        {
            if (posts == null) throw new ArgumentNullException(nameof(posts));
            if (tagName == null) throw new ArgumentNullException(nameof(tagName));
            if (tagUrl == null) throw new ArgumentNullException(nameof(tagUrl));

            //resolve to array so it doesn't double lookup
            Posts = posts.ToArray();
            TagName = tagName;
            var safeEncoded = tagUrl.SafeEncodeUrlSegments();
            TagUrl = safeEncoded.Contains("//") ? safeEncoded : safeEncoded.EnsureStartsWith('/');
            if (count > -1)
                _count = count;
        }

        public IEnumerable<PostModel> Posts { get; }
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