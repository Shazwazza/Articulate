using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Articulate.Models
{
    public class TagListModel : IMasterModel
    {
        public TagListModel(IMasterModel masterModel, string name, IEnumerable<TagModel> tags)
        {
            Theme = masterModel.Theme;
            RootBlogNode = masterModel.RootBlogNode;
            BlogListNode = masterModel.BlogListNode;
            Name = name;
            BlogTitle = masterModel.BlogTitle;
            BlogDescription = masterModel.BlogDescription;
            RootUrl = masterModel.RootUrl;
            Tags = tags;
        }

        public string Theme { get; private set; }
        public IPublishedContent RootBlogNode { get; private set; }
        public IPublishedContent BlogListNode { get; private set; }
        public string Name { get; private set; }
        public string BlogTitle { get; private set; }
        public string BlogDescription { get; private set; }
        public string RootUrl { get; private set; }
        public string ArchiveUrl { get; private set; }
        public IEnumerable<TagModel> Tags { get; private set; } 
    }
}