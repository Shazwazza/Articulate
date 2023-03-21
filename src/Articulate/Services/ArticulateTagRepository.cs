using System;
using System.Collections.Generic;
using System.Linq;
using Articulate.Models;
using NPoco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Infrastructure.Persistence.Repositories.Implement;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Web.Common;
using Umbraco.Extensions;

namespace Articulate.Services
{

    internal class ArticulateTagRepository : RepositoryBase, IArticulateTagRepository
    {
        private readonly IPublishedValueFallback _publishedValueFallback;
        private readonly IVariationContextAccessor _variationContextAccessor;
        private readonly IImageUrlGenerator _imageUrlGenerator;

        public ArticulateTagRepository(
            IScopeAccessor scopeAccessor,
            AppCaches appCaches,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            IImageUrlGenerator imageUrlGenerator) : base(scopeAccessor, appCaches)
        {
            _publishedValueFallback = publishedValueFallback;
            _variationContextAccessor = variationContextAccessor;
            _imageUrlGenerator = imageUrlGenerator;
        }

        /// <summary>
        /// Returns a list of all categories belonging to this articualte root
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <returns></returns>
        public IEnumerable<string> GetAllCategories(
            IMasterModel masterModel)
        {
            //TODO: We want to use the core for this but it's not available, this needs to be implemented: http://issues.umbraco.org/issue/U4-9290

            var sql = GetTagQuery($"{Constants.DatabaseSchema.Tables.Tag}.id, {Constants.DatabaseSchema.Tables.Tag}.tag, {Constants.DatabaseSchema.Tables.Tag}.[group], Count(*) as NodeCount", masterModel)
                .Where($"{Constants.DatabaseSchema.Tables.Tag}." + SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup", new
                {
                    tagGroup = "ArticulateCategories"
                })
                .GroupBy($"{Constants.DatabaseSchema.Tables.Tag}.id", $"{Constants.DatabaseSchema.Tables.Tag}.tag", $"{Constants.DatabaseSchema.Tables.Tag}." + SqlSyntax.GetQuotedColumnName("group") + @"");

            var results = Database.Fetch<TagDto>(sql).Select(x => x.Tag).WhereNotNull().OrderBy(x => x);

            return results;
        }

        public IEnumerable<PostsByTagModel> GetContentByTags(
            UmbracoHelper helper,
            ITagQuery tagQuery,
            IMasterModel masterModel,
            string tagGroup,
            string baseUrlName)
        {
            TagModel[] tags = tagQuery.GetAllContentTags(tagGroup).ToArray();
            if (!tags.Any())
            {
                return Enumerable.Empty<PostsByTagModel>();
            }

            //TODO: We want to use the core for this but it's not available, this needs to be implemented: http://issues.umbraco.org/issue/U4-9290

            IEnumerable<PostsByTagModel> GetResult()
            {
                var taggedContent = new List<TagDto>();

                //process in groups to not exceed the max SQL params
                foreach (var tagBatch in tags.InGroupsOf(2000))
                {
                    var sql = GetTagQuery($"{Constants.DatabaseSchema.Tables.TagRelationship}.nodeId, {Constants.DatabaseSchema.Tables.TagRelationship}.tagId, {Constants.DatabaseSchema.Tables.Tag}.tag", masterModel)
                        .Where("tagId IN (@tagIds) AND cmsTags." + SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup", new
                        {
                            tagIds = tagBatch.Select(x => x.Id).ToArray(),
                            tagGroup = tagGroup
                        });

                    var dbTags = Database.Fetch<TagDto>(sql);

                    taggedContent.AddRange(dbTags);
                }

                var result = new List<PostsByTagModel>();
                foreach (var groupedTags in taggedContent.GroupBy(x => x.TagId))
                {
                    //will be the same tag name for all of these tag Ids
                    var tagName = groupedTags.First().Tag;

                    var publishedContent = helper.Content(groupedTags.Select(t => t.NodeId).Distinct()).WhereNotNull();

                    var model = new PostsByTagModel(
                        publishedContent.Select(c => new PostModel(c, _publishedValueFallback, _variationContextAccessor, _imageUrlGenerator)).OrderByDescending(c => c.PublishedDate),
                        tagName,
                        masterModel.RootBlogNode.Url().EnsureEndsWith('/') + baseUrlName + "/" + tagName.ToLowerInvariant());

                    result.Add(model);
                }

                return result.OrderBy(x => x.TagName).ToArray();
            }

#if DEBUG
            return GetResult();
#else
            //cache this result for a short amount of time
            return (IEnumerable<PostsByTagModel>)AppCaches.RuntimeCache.Get(
                string.Concat(typeof(UmbracoHelperExtensions).Name, "GetContentByTags", masterModel.RootBlogNode.Id, tagGroup),
                GetResult, TimeSpan.FromSeconds(30));
#endif

        }

        public PostsByTagModel GetContentByTag(
            UmbracoHelper helper,
            IMasterModel masterModel,
            string tag,
            string tagGroup,
            string baseUrlName,
            long page,
            long pageSize)
        {
            //TODO: We want to use the core for this but it's not available, this needs to be implemented: http://issues.umbraco.org/issue/U4-9290

            PostsByTagModel GetResult()
            {
                var sqlTags = GetTagQuery($"{Constants.DatabaseSchema.Tables.Node}.id", masterModel);

                //For whatever reason, SQLCE and even SQL SERVER are not willing to lookup 
                //tags with hyphens in them, it's super strange, so we force the tag column to be - what it already is!! what tha.

                sqlTags.Where($"CAST({Constants.DatabaseSchema.Tables.Tag}.tag AS NVARCHAR(200)) = @tagName AND {Constants.DatabaseSchema.Tables.Tag}." + SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup", new
                {
                    tagName = tag,
                    tagGroup = tagGroup
                });

                //get the publishedDate property type id on the ArticulatePost content type
                var publishedDatePropertyTypeId = Database.ExecuteScalar<int>($@"SELECT {Constants.DatabaseSchema.Tables.PropertyType}.id FROM {Constants.DatabaseSchema.Tables.ContentType}
INNER JOIN {Constants.DatabaseSchema.Tables.PropertyType} ON {Constants.DatabaseSchema.Tables.PropertyType}.contentTypeId = {Constants.DatabaseSchema.Tables.ContentType}.nodeId
WHERE {Constants.DatabaseSchema.Tables.ContentType}.alias = @contentTypeAlias AND {Constants.DatabaseSchema.Tables.PropertyType}.alias = @propertyTypeAlias", new { contentTypeAlias = "ArticulatePost", propertyTypeAlias = "publishedDate" });

                var sqlContent = GetContentByTagQueryForPaging($"{Constants.DatabaseSchema.Tables.Node}.id, {Constants.DatabaseSchema.Tables.PropertyData}.dateValue", masterModel, publishedDatePropertyTypeId);

                sqlContent.Append($"WHERE ({Constants.DatabaseSchema.Tables.Node}.id IN (").Append(sqlTags).Append("))");

                //order by the dateValue field which will be the publishedDate 
                sqlContent.OrderBy($"({Constants.DatabaseSchema.Tables.PropertyData}.dateValue) DESC");

                //Put on a single line! NPoco paging does weird stuff on multiline
                sqlContent = SqlContext.Sql(sqlContent.SQL.ToSingleLine(), sqlContent.Arguments);

                //TODO: ARGH This still returns multiple non distinct Ids :(

                var taggedContent = Database.Page<int>(page, pageSize, sqlContent);

                var result = new List<PostsByTagModel>();

                var publishedContent = helper.Content(taggedContent.Items).WhereNotNull();

                var model = new PostsByTagModel(
                    publishedContent.Select(c => new PostModel(c, _publishedValueFallback, _variationContextAccessor, _imageUrlGenerator)),
                    tag,
                    masterModel.RootBlogNode.Url().EnsureEndsWith('/') + baseUrlName + "/" + tag.ToLowerInvariant(),
                    Convert.ToInt32(taggedContent.TotalItems));

                result.Add(model);

                return result.FirstOrDefault();
            }

#if DEBUG
            return GetResult();
#else
            //cache this result for a short amount of time
            
            return (PostsByTagModel)AppCaches.RuntimeCache.Get(
                string.Concat(typeof(UmbracoHelperExtensions).Name, "GetContentByTag", masterModel.RootBlogNode.Id, tagGroup, tag, page, pageSize),
                GetResult, TimeSpan.FromSeconds(30));
#endif
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
        private Sql GetContentByTagQueryForPaging(string selectCols, IMasterModel masterModel, int publishedDatePropertyTypeId)
        {
            var sql = new Sql()
                .Select(selectCols)
                .From(Constants.DatabaseSchema.Tables.Node)
                .InnerJoin(Constants.DatabaseSchema.Tables.Document)
                .On($"{Constants.DatabaseSchema.Tables.Document}.nodeId = {Constants.DatabaseSchema.Tables.Node}.id")
                .InnerJoin(Constants.DatabaseSchema.Tables.ContentVersion)
                .On($"{Constants.DatabaseSchema.Tables.ContentVersion}.nodeId = {Constants.DatabaseSchema.Tables.Document}.nodeId")
                .InnerJoin(Constants.DatabaseSchema.Tables.DocumentVersion)
                .On($"{Constants.DatabaseSchema.Tables.DocumentVersion}.id = {Constants.DatabaseSchema.Tables.ContentVersion}.id")
                .InnerJoin(Constants.DatabaseSchema.Tables.PropertyData)
                .On($"{Constants.DatabaseSchema.Tables.PropertyData}.versionId = {Constants.DatabaseSchema.Tables.DocumentVersion}.id")
                .Where($"{Constants.DatabaseSchema.Tables.Node}.nodeObjectType = @nodeObjectType", new { nodeObjectType = Constants.ObjectTypes.Document })
                //Must be published, this will ensure there's only one version selected
                .Where($"{Constants.DatabaseSchema.Tables.Document}.published = 1")
                .Where($"{Constants.DatabaseSchema.Tables.DocumentVersion}.published = 1")
                //must only return rows with the publishedDate property data so we only get one row and so we can sort on `cmsPropertyData.dateValue` which will be the publishedDate
                .Where($"{Constants.DatabaseSchema.Tables.PropertyData}.propertytypeid = @propTypeId", new { propTypeId = publishedDatePropertyTypeId })
                //only get nodes underneath the current articulate root
                .Where($"{Constants.DatabaseSchema.Tables.Node}." + SqlSyntax.GetQuotedColumnName("path") + " LIKE @path", new { path = masterModel.RootBlogNode.Path + ",%" });
            return sql;
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
        private Sql GetTagQuery(string selectCols, IMasterModel masterModel)
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
                .Where($"{Constants.DatabaseSchema.Tables.Node}." + SqlSyntax.GetQuotedColumnName("path") + " LIKE @path", new { path = masterModel.RootBlogNode.Path + ",%" });
            return sql;
        }

        private class TagDto
        {
            public int NodeId { get; set; }
            public int TagId { get; set; }
            public string Tag { get; set; }
        }
    }
}
