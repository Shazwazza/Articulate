#nullable enable
using System.Collections;

namespace Articulate.Models
{
    /// <summary>
    ///     A collection of <see cref="PostsByTagModel" />.
    /// </summary>
    public class PostTagCollection(IEnumerable<PostsByTagModel> tags) : IEnumerable<PostsByTagModel>
    {
        private int? _maxCount;

        /// <inheritdoc />
        public IEnumerator<PostsByTagModel> GetEnumerator() => tags.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        ///     Gets the weight of a tag for cloud visualization.
        /// </summary>
        public int GetTagWeight(PostsByTagModel postsByTag, decimal maxWeight)
        {
            _maxCount ??= this.DefaultIfEmpty().Max(x => x?.PostCount ?? 0);

            if (_maxCount.Value == 0)
            {
                return 0;
            }

            return Convert.ToInt32(Math.Ceiling(postsByTag.PostCount * maxWeight / _maxCount.Value));
        }
    }
}
