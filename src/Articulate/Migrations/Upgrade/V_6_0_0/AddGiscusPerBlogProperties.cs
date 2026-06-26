#nullable enable
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Articulate.Migrations.Upgrade.V_6_0_0;

/// <summary>
/// Adds four Giscus per-blog text properties (<c>giscusRepo</c>, <c>giscusRepoId</c>,
/// <c>giscusCategory</c>, <c>giscusCategoryId</c>) to the existing <c>blog</c> tab of the
/// Articulate doc type, after the existing fields (SortOrder 6–9, next free block after
/// <c>googleAnalyticsName</c> at SortOrder 5). The SortOrder values are kept in lockstep with
/// <c>src/Articulate/Packaging/package.zip</c> so fresh installs and migrated installs
/// converge on the same property identifiers and tab order. Existing blogs get empty values,
/// so behavior is unchanged until an operator fills the fields or sets the matching appsettings.
/// Idempotent: properties whose alias already exists in the tab are skipped.
/// </summary>
public class AddGiscusPerBlogProperties(
        IMigrationContext context,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IShortStringHelper shortStringHelper,
        ILogger<AddGiscusPerBlogProperties> logger)
    : AsyncMigrationBase(context)
{
    internal const string BlogTabAlias = "blog";

    /// <summary>
    /// The four Giscus per-blog properties, in tab sort order. Keys match the package.xml
    /// GUIDs so fresh installs and migrated installs converge on the same property identifiers.
    /// </summary>
    internal static readonly (string Alias, string Name, int SortOrder, Guid Key)[] GiscusProperties =
    {
        // SortOrders are kept in lockstep with package.xml so fresh installs and migrated
        // installs produce identical tab order. Slots 0–5 are taken by blogDescription,
        // customRssFeedUrl, blogTitle, disqusShortname, googleAnalyticsId, googleAnalyticsName.
        ("giscusRepo",       "Giscus Repo",        6, new Guid("e7bf75bc-9e1d-52d9-a36b-5e43dfcfbd42")),
        ("giscusRepoId",     "Giscus Repo Id",     7, new Guid("04404ee8-30a2-50a2-967d-042b6000d5c4")),
        ("giscusCategory",   "Giscus Category",    8, new Guid("851a03e2-9ac3-5c8a-9222-d2919beb0fb5")),
        ("giscusCategoryId", "Giscus Category Id", 9, new Guid("a1b66f3e-248e-5c83-a6e7-870e91d44fc9")),
    };

    /// <inheritdoc/>
    protected override async Task MigrateAsync()
    {
        IContentType? contentType = contentTypeService.Get(ArticulateConstants.ContentType.Articulate);
        if (contentType is null)
        {
            logger.LogWarning("Articulate content type not found; skipping Giscus per-blog properties migration.");
            return;
        }

        PropertyGroup? blogGroup = contentType.PropertyGroups
            .FirstOrDefault(g => g.Alias == BlogTabAlias);

        if (blogGroup is null)
        {
            logger.LogWarning("Articulate 'blog' tab not found; skipping Giscus per-blog properties migration.");
            return;
        }

        IDataType? textStringDataType = await dataTypeService.GetAsync(Constants.DataTypes.Guids.TextstringGuid);
        if (textStringDataType is null)
        {
            logger.LogError("Textstring data type not found; cannot add Giscus per-blog properties.");
            return;
        }

        // Build the new property types first; idempotent on alias.
        var newProperties = new List<IPropertyType>();
        foreach ((string alias, string name, int sortOrder, Guid key) in GiscusProperties)
        {
            if (blogGroup.PropertyTypes is not null &&
                blogGroup.PropertyTypes.Any(p => p.Alias == alias))
            {
                continue;
            }

            var propertyType = new PropertyType(shortStringHelper, textStringDataType)
            {
                Alias = alias,
                Name = name,
                SortOrder = sortOrder,
                Key = key,
            };
            newProperties.Add(propertyType);
        }

        if (newProperties.Count == 0)
        {
            return;
        }

        // Follow the v18 editing-service pattern (ContentTypeEditingServiceBase.SyncContentTypeProperties):
        // replace the tab's property collection with the merged set. The collection setter
        // wires each property's PropertyGroupId to this group automatically.
        IReadOnlyList<IPropertyType> existing = (blogGroup.PropertyTypes ?? Enumerable.Empty<IPropertyType>()).ToList();
        bool supportsPublishing = existing.FirstOrDefault()?.SupportsPublishing ?? true;
        blogGroup.PropertyTypes = new PropertyTypeCollection(supportsPublishing, existing.Concat(newProperties));

        await contentTypeService.UpdateAsync(contentType, Constants.Security.SuperUserKey);
        logger.LogInformation("Added {Count} Giscus per-blog properties to Articulate 'blog' tab.", newProperties.Count);
    }
}
