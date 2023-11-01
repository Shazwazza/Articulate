using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Models
{
    public class TagListModel : MasterModel
    {
        public TagListModel(            
            IMasterModel masterModel,
            string name,
            int pageSize,
            PostTagCollection tags,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor)
            : base(masterModel.RootBlogNode, publishedValueFallback, variationContextAccessor)
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
