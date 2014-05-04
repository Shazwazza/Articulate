using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Core.Persistence;
using Umbraco.Web;

namespace Articulate
{
    public static class UmbracoHelperExtensions
    {

        public static IEnumerable<TagModel> GetContentByTags(this UmbracoHelper helper, IMasterModel masterModel)
        {
            var tags = helper.TagQuery.GetAllContentTags("ArticulateTags");

            //TODO: Umbraco core needs to have a method to get content by tag(s), in the meantime we 
            // need to run a query
            var sql = new Sql().Select("cmsTagRelationship.nodeId, cmsTagRelationship.tagId, cmsTags.tag")
                .From("cmsTagRelationship")
                .InnerJoin("cmsTags")
                .On("cmsTagRelationship.tagId = cmsTags.id")
                .Where("tagId IN (@tagIds)", new { tagIds = tags.Select(x => x.Id).ToArray() });

            var taggedContent = ApplicationContext.Current.DatabaseContext.Database.Fetch<TagDto>(sql);
            return taggedContent.GroupBy(x => x.TagId)
                .Select(x => new TagModel(
                    helper.TypedContent(x.Select(t => t.NodeId).Distinct()).Select(c => new PostModel(c)),
                    x.First().Tag,
                    masterModel.RootUrl.EnsureEndsWith('/') + "tags/" + x.First().Tag.ToLowerInvariant()));

        }

        private class TagDto
        {
            public int NodeId { get; set; }
            public int TagId { get; set; }
            public string Tag { get; set; }
        }

    }
}
