using System.Collections.Generic;
using Articulate.Models;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Web.Common;

namespace Articulate.Services
{
    public interface IArticulateTagRepository
    {
        IEnumerable<string> GetAllCategories(IMasterModel masterModel);
        IEnumerable<PostsByTagModel> GetContentByTags(UmbracoHelper helper, ITagQuery tagQuery, IMasterModel masterModel, string tagGroup, string baseUrlName);
        PostsByTagModel GetContentByTag(UmbracoHelper helper, IMasterModel masterModel, string tag, string tagGroup, string baseUrlName, long page, long pageSize);
    }
}
