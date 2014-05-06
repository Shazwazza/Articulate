using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Web;

namespace Articulate
{
    public static class UmbracoHelperExtensions
    {

        public static IEnumerable<PostsByTagModel> GetContentByTags(this UmbracoHelper helper, IMasterModel masterModel, string tagGroup)
        {
            var tags = helper.TagQuery.GetAllContentTags(tagGroup);

            //TODO: Umbraco core needs to have a method to get content by tag(s), in the meantime we 
            // need to run a query
            var sql = new Sql().Select("cmsTagRelationship.nodeId, cmsTagRelationship.tagId, cmsTags.tag")
                .From("cmsTagRelationship")
                .InnerJoin("cmsTags")
                .On("cmsTagRelationship.tagId = cmsTags.id")
                .Where("tagId IN (@tagIds) AND cmsTags." + SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumnName("group") + " = @tagGroup", new
                {
                    tagIds = tags.Select(x => x.Id).ToArray(),
                    tagGroup = tagGroup
                });

            var taggedContent = ApplicationContext.Current.DatabaseContext.Database.Fetch<TagDto>(sql);
            return taggedContent.GroupBy(x => x.TagId)
                .Select(x => new PostsByTagModel(
                    helper.TypedContent(
                        x.Select(t => t.NodeId).Distinct()).Select(c => new PostModel(c)).OrderBy(c => c.PublishedDate),
                    x.First().Tag,
                    masterModel.RootUrl.EnsureEndsWith('/') + "tags/" + x.First().Tag.ToLowerInvariant()));
        }

        public static PostsByTagModel GetContentByTag(this UmbracoHelper helper, IMasterModel masterModel, string tag, string tagGroup)
        {
            //TODO: Umbraco core needs to have a method to get content by tag(s), in the meantime we 
            // need to run a query
            var sql = new Sql().Select("cmsTagRelationship.nodeId, cmsTagRelationship.tagId, cmsTags.tag")
                .From("cmsTagRelationship")
                .InnerJoin("cmsTags")
                .On("cmsTagRelationship.tagId = cmsTags.id")
                .Where("cmsTags.tag = @tagName AND cmsTags." + SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumnName("group") + " = @tagGroup", new
                {
                    tagName = tag,
                    tagGroup = tagGroup
                });

            var taggedContent = ApplicationContext.Current.DatabaseContext.Database.Fetch<TagDto>(sql);
            return taggedContent.GroupBy(x => x.TagId)
                .Select(x => new PostsByTagModel(
                    helper.TypedContent(
                        x.Select(t => t.NodeId).Distinct()).Select(c => new PostModel(c)).OrderBy(c => c.PublishedDate),
                    x.First().Tag,
                    masterModel.RootUrl.EnsureEndsWith('/') + "tags/" + x.First().Tag.ToLowerInvariant()))
                .FirstOrDefault();
        }

        private class TagDto
        {
            public int NodeId { get; set; }
            public int TagId { get; set; }
            public string Tag { get; set; }
        }

    }
}
