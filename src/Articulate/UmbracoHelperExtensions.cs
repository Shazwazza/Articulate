using Articulate.Models;
using NPoco;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.XPath;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Web;

namespace Articulate
{

    public static class UmbracoHelperExtensions
    {
        /// <summary>
        /// A method that will return number of posts
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="articulateArchiveIds"></param>
        /// <returns></returns>
        public static int GetPostCount(this UmbracoHelper helper, params int[] articulateArchiveIds)
        {
            var totalPosts = articulateArchiveIds
                .Select(helper.Content)
                .WhereNotNull()
                .SelectMany(x => x.Descendants())
                .Count();

            return totalPosts;
        }

        /// <summary>
        /// A method that will return number of posts
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="authorName"></param>
        /// <param name="articulateArchiveIds"></param>
        /// <returns></returns>
        public static int GetPostCount(this UmbracoHelper helper, string authorName, params int[] articulateArchiveIds)
        {
            var totalPosts = articulateArchiveIds
                .Select(helper.Content)
                .WhereNotNull()
                .SelectMany(x => x.Descendants().Where(d => d.Value<string>("author") == authorName))
                .Count();

            return totalPosts;
        }

        /// <summary>
        /// A method that will return the posts sorted by published date in an efficient way
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="articulateArchiveIds"></param>
        /// <param name="pager"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static IEnumerable<IPublishedContent> GetPostsSortedByPublishedDate(
            this UmbracoHelper helper, 
            PagerModel pager,
            Func<IPublishedContent, bool> filter,
            params int[] articulateArchiveIds)
        {
            var posts = articulateArchiveIds
                .Select(helper.Content)
                .WhereNotNull()
                .SelectMany(x => x.Descendants());
            
            //apply a filter if there is one
            if (filter != null)
            {
                posts = posts.Where(filter);
            }

            //now do the ordering
            posts = posts.OrderByDescending(x => x.Value<DateTime>("publishedDate"))
                .Skip(pager.CurrentPageIndex * pager.PageSize)
                .Take(pager.PageSize);

            return posts;
        }

        public static PostTagCollection GetPostTagCollection(this UmbracoHelper helper, IMasterModel masterModel)
        {
            var tagsBaseUrl = masterModel.RootBlogNode.Value<string>("tagsUrlName");

            var contentByTags = helper.GetContentByTags(masterModel, "ArticulateTags", tagsBaseUrl);

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

            var sqlSyntax = Current.SqlContext.SqlSyntax;

            var sql = GetTagQuery($"{Constants.DatabaseSchema.Tables.Tag}.id, {Constants.DatabaseSchema.Tables.Tag}.tag, {Constants.DatabaseSchema.Tables.Tag}.[group], Count(*) as NodeCount", masterModel, sqlSyntax)
                .Where($"{Constants.DatabaseSchema.Tables.Tag}." + sqlSyntax.GetQuotedColumnName("group") + " = @tagGroup", new
                {
                    tagGroup = "ArticulateCategories"
                })
                .GroupBy($"{Constants.DatabaseSchema.Tables.Tag}.id", $"{Constants.DatabaseSchema.Tables.Tag}.tag", $"{Constants.DatabaseSchema.Tables.Tag}." + sqlSyntax.GetQuotedColumnName("group") + @"");

            using(var scope = Current.ScopeProvider.CreateScope())
            {
                var results = scope.Database.Fetch<TagDto>(sql).Select(x => x.Tag).WhereNotNull().OrderBy(x => x);
                scope.Complete();

                return results;
            }
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
            var listNodes = GetListNodes(masterModel);

            var listNodeIds = listNodes.Select(x => x.Id).ToArray();

            var pager = new PagerModel(count, 0, 1);

            var listItems = helper.GetPostsSortedByPublishedDate(pager, null, listNodeIds);

            var rootPageModel = new ListModel(listNodes[0], listItems, pager);
            return rootPageModel.Posts;
        }

        /// <summary>
        /// Returns a list of the most recent posts
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public static IEnumerable<PostModel> GetRecentPosts(this UmbracoHelper helper, IMasterModel masterModel, int page, int pageSize)
        {
            var listNodes = GetListNodes(masterModel);

            var listNodeIds = listNodes.Select(x => x.Id).ToArray();

            var pager = new PagerModel(pageSize, page - 1, 1);

            var listItems = helper.GetPostsSortedByPublishedDate(pager, null, listNodeIds);

            var rootPageModel = new ListModel(listNodes[0], listItems, pager);
            return rootPageModel.Posts;
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
                .From(Constants.DatabaseSchema.Tables.Tag)
                .InnerJoin(Constants.DatabaseSchema.Tables.TagRelationship)
                .On($"{Constants.DatabaseSchema.Tables.TagRelationship}.tagId = {Constants.DatabaseSchema.Tables.Tag}.id")
                .InnerJoin(Constants.DatabaseSchema.Tables.Content)
                .On($"{Constants.DatabaseSchema.Tables.Content}.nodeId = {Constants.DatabaseSchema.Tables.TagRelationship}.nodeId")
                .InnerJoin(Constants.DatabaseSchema.Tables.Node)
                .On($"{Constants.DatabaseSchema.Tables.Node}.id = {Constants.DatabaseSchema.Tables.Content}.nodeId")
                .Where($"{Constants.DatabaseSchema.Tables.Node}.nodeObjectType = @nodeObjectType", new { nodeObjectType = Constants.ObjectTypes.Document })
                //only get nodes underneath the current articulate root
                .Where($"{Constants.DatabaseSchema.Tables.Node}." + sqlSyntax.GetQuotedColumnName("path") + " LIKE @path", new { path = masterModel.RootBlogNode.Path + ",%" });
            return sql;
        }

        /// <summary>
        /// Gets the tag SQL used to retrieve paged posts for particular tags for a given articulate root node
        /// </summary>
        /// <param name="selectCols"></param>
        /// <param name="masterModel"></param>
        /// <param name="sqlSyntax"></param>
        /// <param name="publishedDatePropertyTypeId">
        /// This is needed to perform the sorting on published date,  this is the PK of the property type for publishedDate on the ArticulatePost content type
        /// </param>
        /// <returns></returns>
        /// <remarks>
        /// TODO: We won't need this when this is fixed http://issues.umbraco.org/issue/U4-9290
        /// </remarks>
        private static Sql GetContentByTagQueryForPaging(string selectCols, IMasterModel masterModel, ISqlSyntaxProvider sqlSyntax, int publishedDatePropertyTypeId)
        {
            var sql = new Sql()
                .Select(selectCols)                
                .From(Constants.DatabaseSchema.Tables.Node)
                .InnerJoin(Constants.DatabaseSchema.Tables.Document)
                .On($"{Constants.DatabaseSchema.Tables.Document}.nodeId = {Constants.DatabaseSchema.Tables.Node}.id")
                .InnerJoin(Constants.DatabaseSchema.Tables.PropertyData)
                .On($"{Constants.DatabaseSchema.Tables.PropertyData}.versionId = {Constants.DatabaseSchema.Tables.Document}.versionId")
                .Where($"{Constants.DatabaseSchema.Tables.Node}.nodeObjectType = @nodeObjectType", new { nodeObjectType = Constants.ObjectTypes.Document })
                //Must be published, this will ensure there's only one version selected
                .Where($"{Constants.DatabaseSchema.Tables.Document}.published = 1")
                //must only return rows with the publishedDate property data so we only get one row and so we can sort on `cmsPropertyData.dataDate` which will be the publishedDate
                .Where($"{Constants.DatabaseSchema.Tables.PropertyData}.propertytypeid = @propTypeId", new {propTypeId = publishedDatePropertyTypeId})
                //only get nodes underneath the current articulate root
                .Where($"{Constants.DatabaseSchema.Tables.Node}." + sqlSyntax.GetQuotedColumnName("path") + " LIKE @path", new { path = masterModel.RootBlogNode.Path + ",%" });
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

            var sqlSyntax = Current.SqlContext.SqlSyntax;

            IEnumerable<PostsByTagModel> GetResult()
            {
                var taggedContent = new List<TagDto>();

                //process in groups to not exceed the max SQL params
                foreach (var tagBatch in tags.InGroupsOf(2000))
                {
                    var sql = GetTagQuery($"{Constants.DatabaseSchema.Tables.TagRelationship}.nodeId, {Constants.DatabaseSchema.Tables.TagRelationship}.tagId, {Constants.DatabaseSchema.Tables.Tag}.tag", masterModel, sqlSyntax)
                        .Where("tagId IN (@tagIds) AND cmsTags." + sqlSyntax.GetQuotedColumnName("group") + " = @tagGroup", new
                        {
                            tagIds = tagBatch.Select(x => x.Id).ToArray(),
                            tagGroup = tagGroup
                        });

                    using(var scope = Current.ScopeProvider.CreateScope())
                    {
                        var dbTags = scope.Database.Fetch<TagDto>(sql);
                        scope.Complete();

                        taggedContent.AddRange(dbTags);
                    }
                }

                var result = new List<PostsByTagModel>();
                foreach (var groupedTags in taggedContent.GroupBy(x => x.TagId))
                {
                    //will be the same tag name for all of these tag Ids
                    var tagName = groupedTags.First().Tag;

                    var publishedContent = helper.Content(groupedTags.Select(t => t.NodeId).Distinct()).WhereNotNull();

                    var model = new PostsByTagModel(
                        publishedContent.Select(c => new PostModel(c)).OrderByDescending(c => c.PublishedDate), 
                        tagName, 
                        masterModel.RootBlogNode.Url.EnsureEndsWith('/') + baseUrlName + "/" + tagName.ToLowerInvariant());

                    result.Add(model);
                }

                return result.OrderBy(x => x.TagName).ToArray();
            }

#if DEBUG
            return GetResult();
#else
            //cache this result for a short amount of time
            return (IEnumerable<PostsByTagModel>)Current.AppCaches.RuntimeCache.Get(
                string.Concat(typeof(UmbracoHelperExtensions).Name, "GetContentByTags", masterModel.RootBlogNode.Id, tagGroup),
                GetResult, TimeSpan.FromSeconds(30));
#endif

        }


        internal static PostsByTagModel GetContentByTag(this UmbracoHelper helper, IMasterModel masterModel, string tag, string tagGroup, string baseUrlName, long page, long pageSize)
        {
            //TODO: We want to use the core for this but it's not available, this needs to be implemented: http://issues.umbraco.org/issue/U4-9290

            var sqlSyntax = Current.SqlContext.SqlSyntax;

            PostsByTagModel GetResult()
            {
                var sqlTags = GetTagQuery($"{Constants.DatabaseSchema.Tables.Node}.id", masterModel, sqlSyntax);
                
                //For whatever reason, SQLCE and even SQL SERVER are not willing to lookup 
                //tags with hyphens in them, it's super strange, so we force the tag column to be - what it already is!! what tha.

                sqlTags.Where($"CAST({Constants.DatabaseSchema.Tables.Tag}.tag AS NVARCHAR(200)) = @tagName AND {Constants.DatabaseSchema.Tables.Tag}." + sqlSyntax.GetQuotedColumnName("group") + " = @tagGroup", new
                {
                    tagName = tag,
                    tagGroup = tagGroup
                });

                //get the publishedDate property type id on the ArticulatePost content type
                using (var scope = Current.ScopeProvider.CreateScope())
                {
                    var publishedDatePropertyTypeId = scope.Database.ExecuteScalar<int>($@"SELECT {Constants.DatabaseSchema.Tables.PropertyType}.id FROM {Constants.DatabaseSchema.Tables.ContentType}
INNER JOIN {Constants.DatabaseSchema.Tables.PropertyType} ON {Constants.DatabaseSchema.Tables.PropertyType}.contentTypeId = {Constants.DatabaseSchema.Tables.ContentType}.nodeId
WHERE {Constants.DatabaseSchema.Tables.ContentType}.alias = @contentTypeAlias AND {Constants.DatabaseSchema.Tables.PropertyType}.alias = @propertyTypeAlias", new { contentTypeAlias = "ArticulatePost", propertyTypeAlias = "publishedDate" });

                    var sqlContent = GetContentByTagQueryForPaging($"{Constants.DatabaseSchema.Tables.Node}.id", masterModel, sqlSyntax, publishedDatePropertyTypeId);

                    sqlContent.Append($"WHERE {Constants.DatabaseSchema.Tables.Node}.id IN (").Append(sqlTags).Append(")");

                    //order by the dataDate field which will be the publishedDate 
                    sqlContent.OrderBy($"{Constants.DatabaseSchema.Tables.PropertyData}.dataDate DESC");

                    //TODO: ARGH This still returns multiple non distinct Ids :(

                    var taggedContent = scope.Database.Page<int>(page, pageSize, sqlContent);

                    var result = new List<PostsByTagModel>();

                    var publishedContent = helper.Content(taggedContent.Items).WhereNotNull();

                    var model = new PostsByTagModel(
                        publishedContent.Select(c => new PostModel(c)),
                        tag,
                        masterModel.RootBlogNode.Url.EnsureEndsWith('/') + baseUrlName + "/" + tag.ToLowerInvariant(),
                        Convert.ToInt32(taggedContent.TotalItems));

                    result.Add(model);

                    scope.Complete();

                    return result.FirstOrDefault();

                }
            }
            

#if DEBUG
            return GetResult();
#else
            //cache this result for a short amount of time
            
            return (PostsByTagModel)Current.AppCaches.RuntimeCache.Get(
                string.Concat(typeof(UmbracoHelperExtensions).Name, "GetContentByTag", masterModel.RootBlogNode.Id, tagGroup, tag, page, pageSize),
                GetResult, TimeSpan.FromSeconds(30));
#endif
        }

        internal static IEnumerable<IPublishedContent> GetContentByAuthor(this UmbracoHelper helper, IPublishedContent[] listNodes, string authorName, PagerModel pager)
        {            
            var listNodeIds = listNodes.Select(x => x.Id).ToArray();           

            var postWithAuthor = helper.GetPostsSortedByPublishedDate(pager, x => string.Equals(x.Value<string>("author"), authorName.Replace("-", " "), StringComparison.InvariantCultureIgnoreCase), listNodeIds);

            var rootPageModel = new ListModel(listNodes[0], postWithAuthor, pager);
            return rootPageModel.Posts;
        }

        private static IPublishedContent[] GetListNodes(IMasterModel masterModel)
        {
            var listNodes = masterModel.RootBlogNode.Children(string.Empty, "ArticulateArchive").ToArray();
            if (listNodes.Length == 0)
            {
                throw new InvalidOperationException(
                    "An ArticulateArchive document must exist under the root Articulate document");
            }
            return listNodes;
        }

        internal static IEnumerable<AuthorModel> GetContentByAuthors(this UmbracoHelper helper, IMasterModel masterModel)
        {            
            var authorsNode = GetAuthorsNode(masterModel);
            var authors = authorsNode.Children.ToList();

            //TODO: Should we have a paged result instead of returning everything?
            var pager = new PagerModel(int.MaxValue, 0, 1);

            var listNodes = GetListNodes(masterModel);
            var listNodeIds = listNodes.Select(x => x.Id).ToArray();

            //used to lazily retrieve posts by author - as it turns out however this extra work to do this lazily in case the 
            //Children property of the AuthorModel is not used is a waste because as soon as we get the latest post date for an author it will
            //iterate. Oh well, it's still lazy just in case.
            var lazyAuthorPosts = new Lazy<Dictionary<string, Tuple<IPublishedContent, List<IPublishedContent>>>>(() =>
            {
                var authorNames = authors.Select(x => x.Name).ToArray();

                //this will track author names to document ids
                var postsWithAuthors = new Dictionary<int, Tuple<string, IPublishedContent>>();

                var posts = helper.GetPostsSortedByPublishedDate(pager, x =>
                {
                    //ensure there's an author and one that matches someone in the author list

                    var author = x.Value<string>("author");
                    var hasName = author != null && authorNames.Contains(author);
                    if (hasName)
                    {
                        postsWithAuthors[x.Id] = Tuple.Create(author, authors.First(a => a.Name == author));
                    }
                    return hasName;
                }, listNodeIds);

                //this tracks all documents to an author name/author content
                var authorPosts = new Dictionary<string, Tuple<IPublishedContent, List<IPublishedContent>>>();

                //read forward
                foreach (var post in posts)
                {
                    var authorInfo = postsWithAuthors[post.Id];
                    var authorName = authorInfo.Item1;
                    var authorContent = authorInfo.Item2;
                    if (authorPosts.ContainsKey(authorName))
                    {
                        authorPosts[authorName].Item2.Add(post);
                    }
                    else
                    {
                        authorPosts.Add(authorName, Tuple.Create(authorContent, new List<IPublishedContent> {post}));
                    }
                }
                return authorPosts;
            });
            
            return authors.OrderBy(x => x.Name)
                .Select(x => new AuthorModel(x, GetLazyAuthorPosts(x, lazyAuthorPosts), pager, GetPostCount(helper, x.Name, listNodeIds)));
        }

        /// <summary>
        /// Used to lazily retrieve posts by author
        /// </summary>
        /// <param name="author"></param>
        /// <param name="lazyAuthorPosts"></param>
        /// <returns></returns>
        private static IEnumerable<IPublishedContent> GetLazyAuthorPosts(
            IPublishedContent author, 
            Lazy<Dictionary<string, Tuple<IPublishedContent, List<IPublishedContent>>>> lazyAuthorPosts)
        {
            foreach (var authorPost in lazyAuthorPosts.Value)
            {
                if (authorPost.Value.Item1.Id == author.Id)
                {
                    foreach (var post in authorPost.Value.Item2)
                    {
                        yield return post;
                    }
                }                                
            }
        }

        private static IPublishedContent GetAuthorsNode(IMasterModel masterModel)
        {
            var authorsNode = masterModel.RootBlogNode.Children
                .FirstOrDefault(x => x.ContentType.Alias.InvariantEquals("ArticulateAuthors"));
            if (authorsNode == null)
            {
                throw new InvalidOperationException(
                    "An ArticulateAuthors document must exist under the root Articulate document");
            }

            return authorsNode;
        }
        
        private class TagDto
        {
            public int NodeId { get; set; }
            public int TagId { get; set; }
            public string Tag { get; set; }
        }

    }
}
