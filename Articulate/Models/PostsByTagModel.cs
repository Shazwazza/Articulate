using System;
using System.Collections.Generic;
using Umbraco.Core;

namespace Articulate.Models
{
    public class PostsByTagModel : PostsByModelBase
    {
        public PostsByTagModel(IEnumerable<PostModel> posts, string tagName, string tagUrl)
            : base(posts)
        {
            if (tagName == null) throw new ArgumentNullException("tagName");
            if (tagUrl == null) throw new ArgumentNullException("tagUrl");

            TagName = tagName;

            var safeEncoded = tagUrl.SafeEncodeUrlSegments();

            TagUrl = safeEncoded.Contains("//") ? safeEncoded : safeEncoded.EnsureStartsWith('/');
        }

        public string TagName { get; private set; }
        public string TagUrl { get; private set; }
    }

}