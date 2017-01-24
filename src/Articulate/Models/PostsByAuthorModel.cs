using System.Collections.Generic;

namespace Articulate.Models
{
    public class PostsByAuthorModel : PostsByModelBase
    {
        public PostsByAuthorModel(PostAuthorModel author, IEnumerable<PostModel> posts)
            : base(posts)
        {
            Author = author;
        }

        public PostAuthorModel Author { get; private set; }
    }
}
