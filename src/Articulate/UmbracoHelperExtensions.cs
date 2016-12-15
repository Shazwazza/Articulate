using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Services;
using Umbraco.Core.Cache;
using Umbraco.Web;

namespace Articulate
{

    public static class UmbracoHelperExtensions
    {
        /// <summary>
        /// A method that will return the posts sorted by published date in an efficient way
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="articulateArchiveId"></param>
        /// <returns></returns>
        public static int GetPostCount(this UmbracoHelper helper, int articulateArchiveId)
        {
            var xPathNavigator = helper.UmbracoContext.ContentCache.GetXPathNavigator(false);
            var xPathChildren = $"//* [@id={articulateArchiveId}]/*[@isDoc]";
            //get the count with XPath, this will be the fastest
            var totalPosts = xPathNavigator
                .Select(xPathChildren)
                .Count;
            return totalPosts;
        }

        /// <summary>
        /// A method that will return the posts sorted by published date in an efficient way
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="articulateArchiveId"></param>
        /// <param name="pager"></param>
        /// <returns></returns>
        public static IEnumerable<IPublishedContent> GetPostsSortedByPublishedDate(this UmbracoHelper helper, int articulateArchiveId, PagerModel pager)
        {
            var xPathNavigator = helper.UmbracoContext.ContentCache.GetXPathNavigator(false);
            var xPathChildren = $"//* [@id={articulateArchiveId}]/*[@isDoc]";

            //Filter/Sort the children we're looking for with XML
            var xmlListItems = xPathNavigator.Select(xPathChildren)
                .Cast<XPathNavigator>()
                .OrderByDescending(x =>
                {
                    var publishedDate = DateTime.MinValue;

                    var xmlNode = x.SelectSingleNode("publishedDate [not(@isDoc)]");
                    if (xmlNode == null)
                        return publishedDate;

                    publishedDate = xmlNode.ValueAsDateTime;
                    return publishedDate;
                })
                .Skip(pager.CurrentPageIndex * pager.PageSize)
                .Take(pager.PageSize);

            //Now we can select the IPublishedContent instances by Id
            var listItems = helper.TypedContent(xmlListItems.Select(x => int.Parse(x.GetAttribute("id", ""))));

            return listItems;
        }

        public static PostTagCollection GetPostTagCollection(this UmbracoHelper helper, IMasterModel masterModel)
        {
            var listNode = masterModel.RootBlogNode.Children
               .FirstOrDefault(x => x.DocumentTypeAlias.InvariantEquals("ArticulateArchive"));
            if (listNode == null)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            //create a blog model of the main page
            var rootPageModel = new MasterModel(listNode);

            var tagsBaseUrl = masterModel.RootBlogNode.GetPropertyValue<string>("tagsUrlName");

            var contentByTags = helper.GetContentByTags(rootPageModel, "ArticulateTags", tagsBaseUrl);

            return new PostTagCollection(contentByTags);
        }

        /// <summary>
        /// Returns a list of all categories belonging to this articualte root
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetAllCategories(this UmbracoHelper helper, IMasterModel masterModel)
        {
            //TODO: We want to use the core for this but it's not available, this needs to be implemented: http://issues.umbraco.org/issue/U4-9290

            var appContext = helper.UmbracoContext.Application;
            var sqlSyntax = appContext.DatabaseContext.SqlSyntax;

            var sql = GetTagQuery("cmsTags.id, cmsTags.tag, cmsTags.[group], Count(*) as NodeCount", masterModel, sqlSyntax)
                .Where("cmsTags." + sqlSyntax.GetQuotedColumnName("group") + " = @tagGroup", new
                {
                    tagGroup = "ArticulateCategories"
                })
                .GroupBy("cmsTags.id", "cmsTags.tag", "cmsTags." + sqlSyntax.GetQuotedColumnName("group") + @"");

            return appContext.DatabaseContext.Database.Fetch<TagDto>(sql).Select(x => x.Tag).WhereNotNull().OrderBy(x => x);
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

            var pager = new PagerModel(count, 0, 1);

            var listItems = helper.GetPostsSortedByPublishedDate(listNode.Id, pager);

            var rootPageModel = new ListModel(listNode, listItems, pager);
            return rootPageModel.Children<PostModel>();
        }

        /// <summary>
        /// Gets the basic tag SQL used to retrieve tags for a given articulate root node
        /// </summary>
        /// <param name="selectCols"></param>
        /// <param name="masterModel"></param>
        /// <param name="sqlSyntax"></param>
        /// <returns></returns>
        /// <remarks>
        /// TODO: We won't need this when this is fixed http://issues.umbraco.org/issue/U4-9290
        /// </remarks>
        private static Sql GetTagQuery(string selectCols, IMasterModel masterModel, ISqlSyntaxProvider sqlSyntax)
        {
            var sql = new Sql()
                .Select(selectCols)
                .From("cmsTags")
                .InnerJoin("cmsTagRelationship")
                .On("cmsTagRelationship.tagId = cmsTags.id")
                .InnerJoin("cmsContent")
                .On("cmsContent.nodeId = cmsTagRelationship.nodeId")
                .InnerJoin("umbracoNode")
                .On("umbracoNode.id = cmsContent.nodeId")
                .Where("umbracoNode.nodeObjectType = @nodeObjectType", new {nodeObjectType = Constants.ObjectTypes.Document})
                //only get nodes underneath the current articulate root
                .Where("umbracoNode." + sqlSyntax.GetQuotedColumnName("path") + " LIKE @path", new {path = masterModel.RootBlogNode.Path + ",%"});
            return sql;
        }

        internal static IEnumerable<PostsByTagModel> GetContentByTags(this UmbracoHelper helper, IMasterModel masterModel, string tagGroup, string baseUrlName)
        {
            var tags = helper.TagQuery.GetAllContentTags(tagGroup).ToArray();
            if (!tags.Any())
            {
                return Enumerable.Empty<PostsByTagModel>();
            }

            //TODO: We want to use the core for this but it's not available, this needs to be implemented: http://issues.umbraco.org/issue/U4-9290

            var appContext = helper.UmbracoContext.Application;
            var sqlSyntax = appContext.DatabaseContext.SqlSyntax;

            Func<IEnumerable<PostsByTagModel>> getResult = () =>
            {
                var taggedContent = new List<TagDto>();

                //process in groups to not exceed the max SQL params
                foreach (var tagBatch in tags.InGroupsOf(2000))
                {
                    var sql = GetTagQuery("cmsTagRelationship.nodeId, cmsTagRelationship.tagId, cmsTags.tag", masterModel, sqlSyntax)
                        .Where("tagId IN (@tagIds) AND cmsTags." + sqlSyntax.GetQuotedColumnName("group") + " = @tagGroup", new
                        {
                            tagIds = tagBatch.Select(x => x.Id).ToArray(),
                            tagGroup = tagGroup
                        });
                    taggedContent.AddRange(appContext.DatabaseContext.Database.Fetch<TagDto>(sql));
                }

                var result = new List<PostsByTagModel>();
                foreach (var groupedTags in taggedContent.GroupBy(x => x.TagId))
                {
                    //will be the same tag name for all of these tag Ids
                    var tagName = groupedTags.First().Tag;

                    var publishedContent = helper.TypedContent(groupedTags.Select(t => t.NodeId).Distinct()).WhereNotNull();

                    var model = new PostsByTagModel(
                        publishedContent.Select(c => new PostModel(c)).OrderByDescending(c => c.PublishedDate),
                        tagName,
                        masterModel.RootBlogNode.Url.EnsureEndsWith('/') + baseUrlName + "/" + tagName.ToLowerInvariant());

                    result.Add(model);
                }

                return result.OrderBy(x => x.TagName).ToArray();
            };

#if DEBUG
            return getResult();
#else
            //cache this result for a short amount of time
            return appContext.ApplicationCache.RuntimeCache.GetCacheItem<IEnumerable<PostsByTagModel>>(
                string.Concat(typeof(UmbracoHelperExtensions).Name, "GetContentByTags", masterModel.RootBlogNode.Id, tagGroup),
                getResult, TimeSpan.FromSeconds(30));
#endif

        }


        internal static PostsByTagModel GetContentByTag(this UmbracoHelper helper, IMasterModel masterModel, string tag, string tagGroup, string baseUrlName)
        {
            //TODO: We want to use the core for this but it's not available, this needs to be implemented: http://issues.umbraco.org/issue/U4-9290

            var appContext = helper.UmbracoContext.Application;
            var sqlSyntax = appContext.DatabaseContext.SqlSyntax;

            Func<PostsByTagModel> getResult = () =>
            {
                var sql = GetTagQuery("cmsTagRelationship.nodeId, cmsTagRelationship.tagId, cmsTags.tag", masterModel, sqlSyntax)
                    .Where("cmsTags.tag = @tagName AND cmsTags." + sqlSyntax.GetQuotedColumnName("group") + " = @tagGroup", new
                    {
                        tagName = tag,
                        tagGroup = tagGroup
                    });

                var taggedContent = appContext.DatabaseContext.Database.Fetch<TagDto>(sql);

                var result = new List<PostsByTagModel>();
                foreach (var groupedTags in taggedContent.GroupBy(x => x.TagId))
                {
                    //will be the same tag name for all of these tag Ids
                    var tagName = groupedTags.First().Tag;

                    var publishedContent = helper.TypedContent(groupedTags.Select(t => t.NodeId).Distinct()).WhereNotNull();

                    var model = new PostsByTagModel(
                        publishedContent.Select(c => new PostModel(c)).OrderByDescending(c => c.PublishedDate),
                        tagName,
                        masterModel.RootBlogNode.Url.EnsureEndsWith('/') + baseUrlName + "/" + tagName.ToLowerInvariant());

                    result.Add(model);
                }

                return result.FirstOrDefault();
            };

#if DEBUG
            return getResult();
#else
            //cache this result for a short amount of time
            return appContext.ApplicationCache.RuntimeCache.GetCacheItem<PostsByTagModel>(
                string.Concat(typeof(UmbracoHelperExtensions).Name, "GetContentByTag", masterModel.RootBlogNode.Id, tagGroup),
                getResult, TimeSpan.FromSeconds(30));
#endif
        }

        private class TagDto
        {
            public int NodeId { get; set; }
            public int TagId { get; set; }
            public string Tag { get; set; }
        }

    }
}
