using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

namespace Articulate.Models
{
    public class TagListModel : IMasterModel
    {
        public TagListModel(IMasterModel masterModel, string name, int pageSize, IEnumerable<PostsByTagModel> tags)
        {
            Theme = masterModel.Theme;
            RootBlogNode = masterModel.RootBlogNode;
            BlogArchiveNode = masterModel.BlogArchiveNode;
            Name = name;
            PageSize = pageSize;
            BlogTitle = masterModel.BlogTitle;
            BlogDescription = masterModel.BlogDescription;        
            Tags = tags;
        }

        public string Theme { get; private set; }
        public IPublishedContent RootBlogNode { get; private set; }
        public IPublishedContent BlogArchiveNode { get; private set; }
        public string Name { get; private set; }
        public string BlogTitle { get; private set; }
        public string BlogDescription { get; private set; }
        public int PageSize { get; private set; }

        public IEnumerable<PostsByTagModel> Tags { get; private set; }

        private int? _maxCount;

        /// <summary>
        /// Returns a tag weight based on the current tag collection out of 10
        /// </summary>
        /// <param name="postsByTag"></param>
        /// <returns></returns>
        public int GetTagWeight(PostsByTagModel postsByTag)
        {
            if (_maxCount.HasValue == false)
            {
                _maxCount = Tags.Max(x => x.PostCount);    
            }

            return Convert.ToInt32(Math.Ceiling(postsByTag.PostCount * 10.0 / _maxCount.Value));
        }
    }
}