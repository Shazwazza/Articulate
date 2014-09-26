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
        public static PostTagCollection GetPostTagCollection(this UmbracoHelper helper, IMasterModel masterModel)
        {
            var listNode = masterModel.RootBlogNode.Children
               .FirstOrDefault(x => x.DocumentTypeAlias.InvariantEquals("ArticulateArchive"));
            if (listNode == null)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            //create a blog model of the main page
            var rootPageModel = new ListModel(listNode);

            var tagsBaseUrl = masterModel.RootBlogNode.GetPropertyValue<string>("tagsUrlName");

            var contentByTags = helper.GetContentByTags(rootPageModel, "ArticulateTags", tagsBaseUrl);

            return new PostTagCollection(contentByTags);
        }

        /// <summary>
        /// Returns a list of all categories
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetAllCategories(this UmbracoHelper helper, IMasterModel masterModel)
        {
            //TODO: Make this somehow only lookup tag categories that are relavent to posts underneath the current Articulate root node!

            return helper.TagQuery.GetAllContentTags("ArticulateCategories").Select(x => x.Text);
        }

        /// <summary>
        /// Returns a list of the most recent posts
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static IEnumerable<PostModel> GetRecentPosts(this UmbracoHelper helper, IMasterModel masterModel, int count)
        {
            var listNode = masterModel.RootBlogNode.Children
               .FirstOrDefault(x => x.DocumentTypeAlias.InvariantEquals("ArticulateArchive"));
            if (listNode == null)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            var rootPageModel = new ListModel(listNode, new PagerModel(count, 0, 1));
            return rootPageModel.Children<PostModel>();
        }
     
        internal static IEnumerable<PostsByTagModel> GetContentByTags(this UmbracoHelper helper, IMasterModel masterModel, string tagGroup, string baseUrlName)
        {
            var tags = helper.TagQuery.GetAllContentTags(tagGroup).ToArray();
            if (!tags.Any())
            {
                return Enumerable.Empty<PostsByTagModel>();
            }

            //TODO: Use the new 7.1.2 tags API to do this

            //TODO: This query will also cause problems if/when a site ends up with thousands of tags! It will fail.

            var sql = new Sql()
                .Select("cmsTagRelationship.nodeId, cmsTagRelationship.tagId, cmsTags.tag")
                .From("cmsTags")
                .InnerJoin("cmsTagRelationship")
                .On("cmsTagRelationship.tagId = cmsTags.id")
                .InnerJoin("cmsContent")
                .On("cmsContent.nodeId = cmsTagRelationship.nodeId")
                .InnerJoin("umbracoNode")
                .On("umbracoNode.id = cmsContent.nodeId")
                .Where("umbracoNode.nodeObjectType = @nodeObjectType", new { nodeObjectType = Constants.ObjectTypes.Document })
                //only get nodes underneath the current articulate root
                .Where("umbracoNode." + SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumnName("path") + " LIKE @path", new { path = masterModel.RootBlogNode.Path + ",%" })
                .Where("tagId IN (@tagIds) AND cmsTags." + SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumnName("group") + " = @tagGroup", new
                {
                    tagIds = tags.Select(x => x.Id).ToArray(),
                    tagGroup = tagGroup
                });

            var taggedContent = ApplicationContext.Current.DatabaseContext.Database.Fetch<TagDto>(sql);
            return taggedContent.GroupBy(x => x.TagId)
                .Select(x => new PostsByTagModel(
                    helper.TypedContent(
                        x.Select(t => t.NodeId).Distinct())
                        .WhereNotNull()
                        .Select(c => new PostModel(c)).OrderByDescending(c => c.PublishedDate),
                    x.First().Tag,
                    masterModel.RootBlogNode.Url.EnsureEndsWith('/') + baseUrlName + "/" + x.First().Tag.ToLowerInvariant()))
                .OrderBy(x => x.TagName);
        }


        internal static PostsByTagModel GetContentByTag(this UmbracoHelper helper, IMasterModel masterModel, string tag, string tagGroup, string baseUrlName)
        {
            //TODO: Use the new 7.1.2 tags API to do this

            var sql = new Sql()
                .Select("cmsTagRelationship.nodeId, cmsTagRelationship.tagId, cmsTags.tag")
                .From("cmsTags")
                .InnerJoin("cmsTagRelationship")
                .On("cmsTagRelationship.tagId = cmsTags.id")
                .InnerJoin("cmsContent")
                .On("cmsContent.nodeId = cmsTagRelationship.nodeId")
                .InnerJoin("umbracoNode")
                .On("umbracoNode.id = cmsContent.nodeId")
                .Where("umbracoNode.nodeObjectType = @nodeObjectType", new {nodeObjectType = Constants.ObjectTypes.Document})
                //only get nodes underneath the current articulate root
                .Where("umbracoNode." + SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumnName("path") + " LIKE @path", new {path = masterModel.RootBlogNode.Path + ",%"})
                .Where("cmsTags.tag = @tagName AND cmsTags." + SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumnName("group") + " = @tagGroup", new
                {
                    tagName = tag,
                    tagGroup = tagGroup
                });

            var taggedContent = ApplicationContext.Current.DatabaseContext.Database.Fetch<TagDto>(sql);
            
            return taggedContent.GroupBy(x => x.TagId)
                .Select(x => new PostsByTagModel(
                    helper.TypedContent(
                        x.Select(t => t.NodeId).Distinct())
                        .WhereNotNull()
                        .Select(c => new PostModel(c)).OrderByDescending(c => c.PublishedDate),
                    x.First().Tag,
                    masterModel.RootBlogNode.Url.EnsureEndsWith('/') + baseUrlName + "/" + x.First().Tag.ToLowerInvariant()))
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
