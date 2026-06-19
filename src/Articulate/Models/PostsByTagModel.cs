#nullable enable
namespace Articulate.Models
{
    /// <summary>
    ///     Model for posts grouped by a specific tag or category.
    /// </summary>
    public class PostsByTagModel
    {
        private int? _count;

        public PostsByTagModel(IEnumerable<PostModel> posts, string tagName, string tagUrl, int count = -1)
        {
            ArgumentNullException.ThrowIfNull(posts);

            ArgumentNullException.ThrowIfNull(tagUrl);

            ArgumentNullException.ThrowIfNull(tagName);
            // resolve to array so it doesn't double lookup
            Posts = posts.ToArray();
            TagName = tagName;
            var safeEncoded = tagUrl.SafeEncodeUrlSegments();
            TagUrl = safeEncoded.Contains("//") ? safeEncoded : safeEncoded.EnsureStartsWith('/');
            if (count > -1)
            {
                _count = count;
            }
        }

        /// <summary>
        ///     Posts in this group.
        /// </summary>
        public IEnumerable<PostModel>? Posts { get; }

        /// <summary>
        ///     Name of the tag.
        /// </summary>
        public string TagName { get; }

        /// <summary>
        ///     URL for the tag.
        /// </summary>
        public string TagUrl { get; }

        /// <summary>
        ///     Gets a string that can represent a html id for the tag
        /// </summary>
        public string HtmlId => TagName.SafeEncodeUrlSegments();

        /// <summary>
        ///     Gets the number of posts for this tag.
        /// </summary>
        public int PostCount
        {
            get
            {
                _count ??= (Posts ?? []).Count();

                return _count.Value;
            }
        }
    }
}
