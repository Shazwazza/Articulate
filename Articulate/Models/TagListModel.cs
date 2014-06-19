using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;
using Umbraco.Web.Models;

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

    public class TagListModel : IMasterModel
    {
        public TagListModel(
            IMasterModel masterModel, 
            string name, 
            int pageSize,
            PostTagCollection tags)
        {
            Theme = masterModel.Theme;
            RootBlogNode = masterModel.RootBlogNode;
            BlogArchiveNode = masterModel.BlogArchiveNode;
            Name = name;
            PageSize = pageSize;
            BlogTitle = masterModel.BlogTitle;
            BlogDescription = masterModel.BlogDescription;        
            Tags = tags;
            BlogBanner = masterModel.BlogBanner;
            BlogLogo = masterModel.BlogLogo;
            DisqusShortName = masterModel.DisqusShortName;
            CustomRssFeed = masterModel.CustomRssFeed;
        }

        public string DisqusShortName { get; private set; }
        public string CustomRssFeed { get; private set; }
        public string Theme { get; private set; }
        public IPublishedContent RootBlogNode { get; private set; }
        public IPublishedContent BlogArchiveNode { get; private set; }
        public string Name { get; private set; }
        public string BlogTitle { get; private set; }
        public string BlogDescription { get; private set; }
        public int PageSize { get; private set; }

        public string BlogLogo { get; private set; }
        public string BlogBanner { get; private set; }

        public PostTagCollection Tags { get; private set; }

    }
}