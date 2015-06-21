using System.Collections;
using System.Collections.Generic;

namespace Articulate.Models
{
    public class PostAuthorCollection : IEnumerable<AuthorModel>
    {
        private readonly IEnumerable<AuthorModel> _authors;

        public PostAuthorCollection(IEnumerable<AuthorModel> authors)
        {
            _authors = authors;
        }

        public IEnumerator<AuthorModel> GetEnumerator()
        {
            return _authors.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}