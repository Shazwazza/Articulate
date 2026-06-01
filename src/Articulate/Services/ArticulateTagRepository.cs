using NPoco;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Infrastructure.Persistence.Repositories.Implement;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Web.Common;

#nullable enable
namespace Articulate.Services
{
    /// <summary>
    /// Custom tag repository for Articulate blog posts.
    /// </summary>
    /// <remarks>
    /// Uses custom SQL because Umbraco's <see cref="ITagQuery"/> doesn't support path-scoped queries
    /// (multi-blog), paging, or sorting by publishedDate. Both Tags and Categories are stored as
    /// Umbraco tags with different groups (ArticulateTags and ArticulateCategories).
    /// </remarks>
    internal class ArticulateTagRepository(
        IScopeAccessor scopeAccessor,
        AppCaches appCaches,
        IPublishedValueFallback publishedValueFallback)
        : RepositoryBase(scopeAccessor, appCaches), IArticulateTagRepository
    {
        /// <summary>
        /// Returns a list of all categories belonging to this articulate root
        /// </summary>
        /// <param name="masterModel"></param>
        /// <returns></returns>
        IEnumerable<string> IArticulateTagRepository.GetAllCategories(
            IMasterModel masterModel)
        {
            // Umbraco's ITagQuery does not support the path-scoped, grouped query Articulate needs here.
            Sql sql = GetTagQuery(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.id AS TagId, {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.tag AS Tag, {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.[group] AS [Group], Count(*) as NodeCount",
                    masterModel.RootBlogNode.Path)
                .Where(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}." +
                    SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup",
                    new { tagGroup = ArticulateConstants.DataType.ArticulateCategories, })
                .GroupBy(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.id",
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.tag",
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}." +
                    SqlSyntax.GetQuotedColumnName("group") + string.Empty);

            IOrderedEnumerable<string> results =
                Database.Fetch<TagDto>(sql).Select(x => x.Tag).WhereNotNull().OrderBy(x => x);

            return results;
        }

        IEnumerable<string> IArticulateTagRepository.GetAllTags(string rootPath, string tagGroup)
        {
            return ((IArticulateTagRepository)this).GetAllTagInfos(rootPath, tagGroup).Select(x => x.Name);
        }

        IEnumerable<ArticulateTagInfo> IArticulateTagRepository.GetAllTagInfos(string rootPath, string tagGroup)
        {
            IEnumerable<ArticulateTagInfo> GetResult()
            {
                Sql sql = GetTagQuery(
                        $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.id AS TagId, {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.tag AS Tag, {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.[group] AS [Group]",
                        rootPath)
                    .Where(
                        $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}." +
                        SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup",
                        new { tagGroup })
                    .GroupBy(
                        $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.id",
                        $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.tag",
                        $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}." +
                        SqlSyntax.GetQuotedColumnName("group") + string.Empty);

                return Database.Fetch<TagDto>(sql)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Tag))
                    .Select(x => new ArticulateTagInfo(x.TagId, x.Tag!))
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

#if DEBUG
            return GetResult();
#else
            return (IEnumerable<ArticulateTagInfo>)AppCaches.RuntimeCache.Get(
                string.Concat(
                    typeof(ArticulateTagRepository).Name,
                    nameof(IArticulateTagRepository.GetAllTagInfos),
                    rootPath,
                    tagGroup),
                GetResult,
                TimeSpan.FromSeconds(30))!;
#endif
        }

        /// <inheritdoc/>
        IEnumerable<PostsByTagModel> IArticulateTagRepository.GetContentByTags(
            UmbracoHelper helper,
            ITagQuery tagQuery,
            IMasterModel masterModel,
            string tagGroup,
            string baseUrlName)
        {
            TagModel?[] tags = [.. tagQuery.GetAllContentTags(tagGroup)];
            if (tags.Length == 0)
            {
                return [];
            }

            IEnumerable<PostsByTagModel> GetResult()
            {
                var taggedContent = new List<TagDto>();

                // Process in batches to avoid exceeding max SQL params
                foreach (IEnumerable<TagModel?> tagBatch in tags.InGroupsOf(2000))
                {
                    Sql sql = GetTagQuery(
                            $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.TagRelationship}.nodeId, {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.TagRelationship}.tagId, {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.tag",
                            masterModel.RootBlogNode.Path)
                        .Where(
                            "tagId IN (@tagIds) AND cmsTags." + SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup",
                            new
                            {
                                tagIds = tagBatch.Where(x => x is not null).Select(x => x!.Id).ToArray(), tagGroup,
                            });

                    List<TagDto> dbTags = Database.Fetch<TagDto>(sql);

                    taggedContent.AddRange(dbTags);
                }

                var result = new List<PostsByTagModel>();
                foreach (IGrouping<int, TagDto> groupedTags in taggedContent.GroupBy(x => x.TagId))
                {
                    var tagName = groupedTags.First().Tag;
                    if (tagName is null)
                    {
                        continue;
                    }

                    IEnumerable<IPublishedContent> publishedContent =
                        helper.Content(groupedTags.Select(t => t.NodeId).Distinct()).WhereNotNull();

                    var model = new PostsByTagModel(
                        publishedContent.Select(c => new PostModel(c, publishedValueFallback))
                            .OrderByDescending(c => c.PublishedDate),
                        tagName,
                        masterModel.RootBlogNode.Url().EnsureEndsWith('/') + baseUrlName + "/" +
                        tagName.ToLowerInvariant());

                    result.Add(model);
                }

                return result.OrderBy(x => x.TagName).ToArray();
            }

#if DEBUG
            return GetResult();
#else
            // Cache this result for a short amount of time
            return (IEnumerable<PostsByTagModel>)AppCaches.RuntimeCache.Get(
                string.Concat(
                typeof(UmbracoHelperExtensions).Name,
                "GetContentByTags",
                masterModel.RootBlogNode.Id,
                tagGroup),
                GetResult,
                TimeSpan.FromSeconds(30))!;
#endif
        }

        /// <inheritdoc/>
        PostsByTagModel IArticulateTagRepository.GetContentByTag(
            UmbracoHelper helper,
            IMasterModel masterModel,
            string tag,
            string tagGroup,
            string baseUrlName,
            long page,
            long pageSize)
        {
            PostsByTagModel GetResult()
            {
                Sql sqlTags = GetTagQuery(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node}.id",
                    masterModel.RootBlogNode.Path);

                // Cast to NVARCHAR to handle tags with hyphens
                sqlTags.Where(
                    $"CAST({Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.tag AS NVARCHAR(200)) = @tagName AND {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}." +
                    SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup",
                    new { tagName = tag, tagGroup, });

                // The publishedDate property type ID is schema-level data that only changes on
                // schema migration, so cache it for the app lifetime to avoid a DB round-trip on
                // every tag-page cache miss.
#if DEBUG
                var publishedDatePropertyTypeId = Database.ExecuteScalar<int>(
                    $@"SELECT {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyType}.id FROM {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.ContentType} INNER JOIN {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyType} ON {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyType}.contentTypeId = {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.ContentType}.nodeId WHERE {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.ContentType}.alias = @contentTypeAlias AND {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyType}.alias = @propertyTypeAlias",
                    new
                    {
                        contentTypeAlias = ArticulateConstants.ContentType.ArticulatePost,
                        propertyTypeAlias = "publishedDate"
                    });
#else
                var publishedDatePropertyTypeId = (int)AppCaches.RuntimeCache.Get(
                    $"{typeof(ArticulateTagRepository).Name}_publishedDatePropertyTypeId",
                    () => Database.ExecuteScalar<int>(
                        $@"SELECT {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyType}.id FROM {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.ContentType} INNER JOIN {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyType} ON {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyType}.contentTypeId = {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.ContentType}.nodeId WHERE {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.ContentType}.alias = @contentTypeAlias AND {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyType}.alias = @propertyTypeAlias",
                        new
                        {
                            contentTypeAlias = ArticulateConstants.ContentType.ArticulatePost,
                            propertyTypeAlias = "publishedDate"
                        }),
                    TimeSpan.FromHours(1))!;
#endif

                Sql sqlContent = GetContentByTagQueryForPaging(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node}.id, {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyData}.dateValue",
                    masterModel,
                    publishedDatePropertyTypeId);

                sqlContent.Append($"WHERE ({Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node}.id IN (")
                    .Append(sqlTags).Append("))");


                sqlContent.OrderBy($"({Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyData}.dateValue) DESC");

                // Put on a single line - NPoco paging has issues with multiline SQL
                sqlContent = SqlContext.Sql(sqlContent.SQL.ToSingleLine(), sqlContent.Arguments);

                Page<int> taggedContent = Database.Page<int>(page, pageSize, sqlContent);

                IEnumerable<IPublishedContent> publishedContent = helper.Content(taggedContent.Items).WhereNotNull();

                var model = new PostsByTagModel(
                    publishedContent.Select(c => new PostModel(c, publishedValueFallback)),
                    tag,
                    masterModel.RootBlogNode.Url().EnsureEndsWith('/') + baseUrlName + "/" + tag.ToLowerInvariant(),
                    Convert.ToInt32(taggedContent.TotalItems));

                return model;
            }

#if DEBUG
            return GetResult();
#else
            // Cache this result for a short amount of time
            return (PostsByTagModel)AppCaches.RuntimeCache.Get(
                string.Concat(
                typeof(UmbracoHelperExtensions).Name,
                "GetContentByTag",
                masterModel.RootBlogNode.Id,
                tagGroup,
                tag,
                page,
                pageSize),
                GetResult,
                TimeSpan.FromSeconds(30))!;
#endif
        }


        private Sql GetContentByTagQueryForPaging(
            string selectCols,
            IMasterModel masterModel,
            int publishedDatePropertyTypeId)
        {
            Sql sql = new Sql()
                .Select(selectCols)
                .From(Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node)
                .InnerJoin(Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Document)
                .On(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Document}.nodeId = {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node}.id")
                .InnerJoin(Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.ContentVersion)
                .On(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.ContentVersion}.nodeId = {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Document}.nodeId")
                .InnerJoin(Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.DocumentVersion)
                .On(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.DocumentVersion}.id = {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.ContentVersion}.id")
                .InnerJoin(Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyData)
                .On(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyData}.versionId = {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.DocumentVersion}.id")
                .Where(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node}.nodeObjectType = @nodeObjectType",
                    new { nodeObjectType = Umbraco.Cms.Core.Constants.ObjectTypes.Document })
                // Must be published - ensures only one version is selected
                .Where($"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Document}.published = 1")
                .Where($"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.DocumentVersion}.published = 1")
                // Filter to publishedDate property for sorting
                .Where(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.PropertyData}.propertytypeid = @propTypeId",
                    new { propTypeId = publishedDatePropertyTypeId })
                // Scope to current blog root path (multi-blog support)
                .Where(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node}." +
                    SqlSyntax.GetQuotedColumnName("path") + " LIKE @path",
                    new { path = masterModel.RootBlogNode.Path + ",%" });
            return sql;
        }


        private Sql GetTagQuery(string selectCols, string rootPath)
        {
            Sql sql = new Sql()
                .Select(selectCols)
                .From(Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag)
                .InnerJoin(Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.TagRelationship)
                .On(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.TagRelationship}.tagId = {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Tag}.id")
                .InnerJoin(Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Content)
                .On(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Content}.nodeId = {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.TagRelationship}.nodeId")
                .InnerJoin(Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node)
                .On(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node}.id = {Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Content}.nodeId")
                .Where(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node}.nodeObjectType = @nodeObjectType",
                    new { nodeObjectType = Umbraco.Cms.Core.Constants.ObjectTypes.Document })
                // Scope to current blog root path (multi-blog support)
                .Where(
                    $"{Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.Node}." +
                    SqlSyntax.GetQuotedColumnName("path") + " LIKE @path",
                    new { path = rootPath + ",%" });
            return sql;
        }

        // DTO for NPoco query results
        private class TagDto
        {
            public int NodeId { get; init; }

            public int TagId { get; init; }

            public string? Tag { get; init; }

            public string? Group { get; init; }
        }
    }
}
