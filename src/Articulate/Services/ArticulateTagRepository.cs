#nullable enable
using NPoco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Infrastructure.Persistence.Repositories.Implement;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Web.Common;

namespace Articulate.Services
{
    /// <summary>
    ///     Custom tag repository for Articulate blog posts.
    /// </summary>
    /// <remarks>
    ///     Uses custom SQL because Umbraco's <see cref="ITagQuery" /> doesn't support path-scoped queries
    ///     (multi-blog), paging, or sorting by publishedDate. Both Tags and Categories are stored as
    ///     Umbraco tags with different groups (ArticulateTags and ArticulateCategories).
    /// </remarks>
    internal class ArticulateTagRepository(
        IScopeAccessor scopeAccessor,
        AppCaches appCaches,
        IPublishedValueFallback publishedValueFallback)
        : RepositoryBase(scopeAccessor, appCaches), IArticulateTagRepository
    {
        /// <summary>
        ///     Returns a list of all categories belonging to this articulate root
        /// </summary>
        /// <param name="masterModel"></param>
        /// <returns></returns>
        IEnumerable<string> IArticulateTagRepository.GetAllCategories(
            IMasterModel masterModel)
        {
            // Umbraco's ITagQuery does not support the path-scoped, grouped query Articulate needs here.
            Sql sql = GetTagQuery(
                    $"{Constants.DatabaseSchema.Tables.Tag}.id AS TagId, {Constants.DatabaseSchema.Tables.Tag}.tag AS Tag, {Constants.DatabaseSchema.Tables.Tag}.[group] AS [Group], Count(*) as NodeCount",
                    masterModel.RootBlogNode.Path)
                .Where(
                    $"{Constants.DatabaseSchema.Tables.Tag}." +
                    SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup",
                    new { tagGroup = ArticulateConstants.DataType.ArticulateCategories })
                .GroupBy(
                    $"{Constants.DatabaseSchema.Tables.Tag}.id",
                    $"{Constants.DatabaseSchema.Tables.Tag}.tag",
                    $"{Constants.DatabaseSchema.Tables.Tag}." +
                    SqlSyntax.GetQuotedColumnName("group") + string.Empty);

            IOrderedEnumerable<string> results =
                Database.Fetch<TagDto>(sql).Select(x => x.Tag).WhereNotNull().OrderBy(x => x);

            return results;
        }

        IEnumerable<string> IArticulateTagRepository.GetAllTags(string rootPath, string tagGroup) =>
            ((IArticulateTagRepository)this).GetAllTagInfos(rootPath, tagGroup).Select(x => x.Name);

        IEnumerable<ArticulateTagInfo> IArticulateTagRepository.GetAllTagInfos(string rootPath, string tagGroup)
        {
            IEnumerable<ArticulateTagInfo> GetResult()
            {
                Sql sql = GetTagQuery(
                        $"{Constants.DatabaseSchema.Tables.Tag}.id AS TagId, {Constants.DatabaseSchema.Tables.Tag}.tag AS Tag, {Constants.DatabaseSchema.Tables.Tag}.[group] AS [Group]",
                        rootPath)
                    .Where(
                        $"{Constants.DatabaseSchema.Tables.Tag}." +
                        SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup",
                        new { tagGroup })
                    .GroupBy(
                        $"{Constants.DatabaseSchema.Tables.Tag}.id",
                        $"{Constants.DatabaseSchema.Tables.Tag}.tag",
                        $"{Constants.DatabaseSchema.Tables.Tag}." +
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

        /// <inheritdoc />
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
                            $"{Constants.DatabaseSchema.Tables.TagRelationship}.nodeId, {Constants.DatabaseSchema.Tables.TagRelationship}.tagId, {Constants.DatabaseSchema.Tables.Tag}.tag",
                            masterModel.RootBlogNode.Path)
                        .Where(
                            "tagId IN (@tagIds) AND cmsTags." + SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup",
                            new { tagIds = tagBatch.Where(x => x is not null).Select(x => x!.Id).ToArray(), tagGroup });

                    List<TagDto> dbTags = Database.Fetch<TagDto>(sql);

                    taggedContent.AddRange(dbTags);
                }

                // Hoist constant URL prefix out of the loop: Url() + EnsureEndsWith + baseUrlName
                // are invariant for the duration of the foreach, so computing once avoids N URL
                // provider lookups and N potential string allocations.
                string tagUrlPrefix = masterModel.RootBlogNode.Url().EnsureEndsWith('/') + baseUrlName + "/";
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
                        tagUrlPrefix + tagName.ToLowerInvariant());

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

        /// <inheritdoc />
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
                    $"{Constants.DatabaseSchema.Tables.Node}.id",
                    masterModel.RootBlogNode.Path);

                // Cast to NVARCHAR to handle tags with hyphens
                sqlTags.Where(
                    $"CAST({Constants.DatabaseSchema.Tables.Tag}.tag AS NVARCHAR(200)) = @tagName AND {Constants.DatabaseSchema.Tables.Tag}." +
                    SqlSyntax.GetQuotedColumnName("group") + " = @tagGroup",
                    new { tagName = tag, tagGroup });

                // The publishedDate property type ID is schema-level data that only changes on
                // schema migration, so cache it for the app lifetime to avoid a DB round-trip on
                // every tag-page cache miss.
#if DEBUG
                var publishedDatePropertyTypeId = Database.ExecuteScalar<int>(
                    $@"SELECT {Constants.DatabaseSchema.Tables.PropertyType}.id FROM {Constants.DatabaseSchema.Tables.ContentType} INNER JOIN {Constants.DatabaseSchema.Tables.PropertyType} ON {Constants.DatabaseSchema.Tables.PropertyType}.contentTypeId = {Constants.DatabaseSchema.Tables.ContentType}.nodeId WHERE {Constants.DatabaseSchema.Tables.ContentType}.alias = @contentTypeAlias AND {Constants.DatabaseSchema.Tables.PropertyType}.alias = @propertyTypeAlias",
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
                    $"{Constants.DatabaseSchema.Tables.Node}.id, {Constants.DatabaseSchema.Tables.PropertyData}.dateValue",
                    masterModel,
                    publishedDatePropertyTypeId);

                sqlContent.Append($"WHERE ({Constants.DatabaseSchema.Tables.Node}.id IN (")
                    .Append(sqlTags).Append("))");


                sqlContent.OrderBy($"({Constants.DatabaseSchema.Tables.PropertyData}.dateValue) DESC");

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
            => BuildContentByTagQueryForPaging(
                selectCols,
                masterModel.RootBlogNode.Path,
                publishedDatePropertyTypeId,
                SqlSyntax);

        // Pure builder (no ambient scope dependency) so ArticulateTagRepositorySqlTests can assert
        // the generated SQL shape — multi-blog path scoping, published-only filters, publishedDate
        // property filter, parameterisation — without a database.
        internal static Sql BuildContentByTagQueryForPaging(
            string selectCols,
            string rootPath,
            int publishedDatePropertyTypeId,
            ISqlSyntaxProvider sqlSyntax)
        {
            var pathColumn = $"{Constants.DatabaseSchema.Tables.Node}.{sqlSyntax.GetQuotedColumnName("path")}";
            return new Sql()
                .Select(selectCols)
                .From(Constants.DatabaseSchema.Tables.Node)
                .InnerJoin(Constants.DatabaseSchema.Tables.Document)
                .On(
                    $"{Constants.DatabaseSchema.Tables.Document}.nodeId = {Constants.DatabaseSchema.Tables.Node}.id")
                .InnerJoin(Constants.DatabaseSchema.Tables.ContentVersion)
                .On(
                    $"{Constants.DatabaseSchema.Tables.ContentVersion}.nodeId = {Constants.DatabaseSchema.Tables.Document}.nodeId")
                .InnerJoin(Constants.DatabaseSchema.Tables.DocumentVersion)
                .On(
                    $"{Constants.DatabaseSchema.Tables.DocumentVersion}.id = {Constants.DatabaseSchema.Tables.ContentVersion}.id")
                .InnerJoin(Constants.DatabaseSchema.Tables.PropertyData)
                .On(
                    $"{Constants.DatabaseSchema.Tables.PropertyData}.versionId = {Constants.DatabaseSchema.Tables.DocumentVersion}.id")
                .Where(
                    $"{Constants.DatabaseSchema.Tables.Node}.nodeObjectType = @nodeObjectType",
                    new { nodeObjectType = Constants.ObjectTypes.Document })
                // Must be published - ensures only one version is selected
                .Where($"{Constants.DatabaseSchema.Tables.Document}.published = 1")
                .Where($"{Constants.DatabaseSchema.Tables.DocumentVersion}.published = 1")
                // Filter to publishedDate property for sorting
                .Where(
                    $"{Constants.DatabaseSchema.Tables.PropertyData}.propertytypeid = @propTypeId",
                    new { propTypeId = publishedDatePropertyTypeId })
                // Scope to current blog root path (multi-blog support)
                .Where($"{pathColumn} LIKE @path", new { path = rootPath + ",%" });
        }


        private Sql GetTagQuery(string selectCols, string rootPath)
            => BuildTagQuery(selectCols, rootPath, SqlSyntax);

        // Pure builder — see BuildContentByTagQueryForPaging. Path-scoped, node-object-type-filtered
        // tag join used by every tag/category listing query.
        internal static Sql BuildTagQuery(
            string selectCols,
            string rootPath,
            ISqlSyntaxProvider sqlSyntax)
        {
            var pathColumn = $"{Constants.DatabaseSchema.Tables.Node}.{sqlSyntax.GetQuotedColumnName("path")}";
            return new Sql()
                .Select(selectCols)
                .From(Constants.DatabaseSchema.Tables.Tag)
                .InnerJoin(Constants.DatabaseSchema.Tables.TagRelationship)
                .On(
                    $"{Constants.DatabaseSchema.Tables.TagRelationship}.tagId = {Constants.DatabaseSchema.Tables.Tag}.id")
                .InnerJoin(Constants.DatabaseSchema.Tables.Content)
                .On(
                    $"{Constants.DatabaseSchema.Tables.Content}.nodeId = {Constants.DatabaseSchema.Tables.TagRelationship}.nodeId")
                .InnerJoin(Constants.DatabaseSchema.Tables.Node)
                .On(
                    $"{Constants.DatabaseSchema.Tables.Node}.id = {Constants.DatabaseSchema.Tables.Content}.nodeId")
                .Where(
                    $"{Constants.DatabaseSchema.Tables.Node}.nodeObjectType = @nodeObjectType",
                    new { nodeObjectType = Constants.ObjectTypes.Document })
                // Scope to current blog root path (multi-blog support)
                .Where($"{pathColumn} LIKE @path", new { path = rootPath + ",%" });
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
