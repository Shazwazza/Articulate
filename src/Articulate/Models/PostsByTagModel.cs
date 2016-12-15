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
            if (posts == null) throw new ArgumentNullException(nameof(posts));
            if (tagName == null) throw new ArgumentNullException(nameof(tagName));
            if (tagUrl == null) throw new ArgumentNullException(nameof(tagUrl));

            _posts = posts;
            TagName = tagName;
            var safeEncoded = tagUrl.SafeEncodeUrlSegments();
            TagUrl = safeEncoded.Contains("//") ? safeEncoded : safeEncoded.EnsureStartsWith('/');
        }

        private PostModel[] _resolvedPosts;
        /// <summary>
        /// Lazily resolves the posts to an array
        /// </summary>
        private PostModel[] ResolvedPosts => _resolvedPosts ?? (_resolvedPosts = _posts.ToArray());
        private readonly IEnumerable<PostModel> _posts;

        public IEnumerable<PostModel> Posts => ResolvedPosts;
        public string TagName { get; private set; }
        public string TagUrl { get; private set; }
        public int PostCount => ResolvedPosts.Length;
    }

}