using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Migrations;

#nullable enable

namespace Articulate.Migrations.Upgrade.V_7_0_0;

/// <summary>
/// Moves the ArticulatePost URL and import properties into named editor groups.
/// </summary>
public sealed class OrganizeArticulatePostProperties(
    IMigrationContext context,
    IContentTypeService contentTypeService)
    : AsyncMigrationBase(context)
{
    internal const string SeoGroupAlias = "seo";
    internal const string SystemGroupAlias = "system";

    protected override async Task MigrateAsync()
    {
        IContentType? contentType = contentTypeService.Get(ArticulateConstants.ContentType.ArticulatePost);
        if (contentType is null)
        {
            return;
        }

        bool changed = EnsureGroup(contentType, "SEO", SeoGroupAlias);
        changed |= EnsureGroup(contentType, "System", SystemGroupAlias);
        changed |= MoveProperty(contentType, "umbracoUrlAlias", SeoGroupAlias);
        changed |= MoveProperty(contentType, "importId", SystemGroupAlias);

        if (changed)
        {
            Attempt<ContentTypeOperationStatus> update = await contentTypeService.UpdateAsync(contentType, Constants.Security.SuperUserKey);
            if (!update.Success)
            {
                throw new InvalidOperationException(
                    $"Failed updating ArticulatePost content type: {update.Result}",
                    update.Exception);
            }
        }
    }

    private static bool EnsureGroup(IContentType contentType, string name, string alias)
    {
        if (contentType.PropertyGroups.Any(group => group.Alias == alias))
        {
            return false;
        }

        // AddPropertyGroup updates the content type's pending changes, but the current
        // PropertyGroups snapshot does not necessarily contain the new group yet.
        contentType.AddPropertyGroup(alias, name);
        return true;
    }

    private static bool MoveProperty(IContentType contentType, string propertyAlias, string groupAlias)
    {
        IPropertyType? property = contentType.PropertyTypes.FirstOrDefault(property => property.Alias == propertyAlias);
        if (property is null)
        {
            return false;
        }

        PropertyGroup? group = contentType.PropertyGroups.FirstOrDefault(group => group.Alias == groupAlias);
        if (group?.PropertyTypes?.Any(groupProperty => groupProperty.Alias == propertyAlias) == true)
        {
            return false;
        }

        contentType.MovePropertyType(propertyAlias, groupAlias);
        return true;
    }
}
