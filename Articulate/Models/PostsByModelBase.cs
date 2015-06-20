using System;
using System.Collections.Generic;
using System.Linq;

namespace Articulate.Models
{
    using Umbraco.Core;

    public abstract class PostsByModelBase
    {
        protected PostsByModelBase(IEnumerable<PostModel> posts)
        {
            if (posts == null) throw new ArgumentNullException("posts");

            //resolve to array so it doesn't double lookup
            Posts = posts.ToArray();
        }

        public IEnumerable<PostModel> Posts { get; private set; }

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

        protected string GetSaveEncodedUrl(string url)
        {
            var safeEncoded = url.SafeEncodeUrlSegments();

            return safeEncoded.Contains("//") ? safeEncoded : safeEncoded.EnsureStartsWith('/');
        }
    }

}