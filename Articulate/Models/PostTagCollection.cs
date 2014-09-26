using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Articulate.Models
{
    public class PostTagCollection : IEnumerable<PostsByTagModel>
    {
        private readonly IEnumerable<PostsByTagModel> _tags;

        public PostTagCollection(IEnumerable<PostsByTagModel> tags)
        {
            _tags = tags;
        }

        private int? _maxCount;

        /// <summary>
        /// Returns a tag weight based on the current tag collection out of x
        /// </summary>
        /// <param name="postsByTag"></param>
        /// <param name="maxWeight"></param>
        /// <returns></returns>
        public int GetTagWeight(PostsByTagModel postsByTag, decimal maxWeight)
        {
            if (_maxCount.HasValue == false)
            {
                _maxCount = this.Max(x => x.PostCount);
            }
            return Convert.ToInt32(Math.Ceiling(postsByTag.PostCount * maxWeight / _maxCount.Value));
        }

        public IEnumerator<PostsByTagModel> GetEnumerator()
        {
            return _tags.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}