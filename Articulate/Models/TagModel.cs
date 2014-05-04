using System.Collections;
using System.Collections.Generic;

namespace Articulate.Models
{
    public class TagModel
    {
        public TagModel(IEnumerable<PostModel> posts, string tagName, string tagUrl)
        {
            Posts = posts;
            TagName = tagName;
            TagUrl = tagUrl;
        }

        public IEnumerable<PostModel> Posts { get; private set; }

        public string TagName { get; private set; }
        //public int PageCount { get; private set; }
        public string TagUrl { get; private set; }
    }

}