using Umbraco.Core.Models;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class TagListModel : MasterModel
    {
        public TagListModel(
            IMasterModel masterModel, 
            string name, 
            int pageSize,
            PostTagCollection tags)
            : base(masterModel.RootBlogNode)
        {
            Name = name;
            Theme = masterModel.Theme;
            RootBlogNode = masterModel.RootBlogNode;
            BlogArchiveNode = masterModel.BlogArchiveNode;
            PageSize = pageSize;
            BlogTitle = masterModel.BlogTitle;
            BlogDescription = masterModel.BlogDescription;        
            Tags = tags;
            BlogBanner = masterModel.BlogBanner;
            BlogLogo = masterModel.BlogLogo;
            DisqusShortName = masterModel.DisqusShortName;
            CustomRssFeed = masterModel.CustomRssFeed;
            PageTitle = Name + " - " + BlogTitle;
        }

        public PostTagCollection Tags { get; }

        public override string Name { get; }
    }
}