using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Articulate.Migrations.Upgrade.V_6_0_0
{
    public class MakeBlogUrlNamesOptional(IMigrationContext context, IContentTypeService contentTypeService)
        : AsyncMigrationBase(context)
    {
        private static readonly string[] _optionalRouteAliases = ["categoriesUrlName", "tagsUrlName"];

        protected override async Task MigrateAsync()
        {
            IContentType articulateContentType = contentTypeService.Get(ArticulateConstants.ContentType.Articulate);
            if (articulateContentType == null)
            {
                return;
            }

            var changed = false;
            foreach (var propertyAlias in _optionalRouteAliases)
            {
                IPropertyType routeProperty =
                    articulateContentType.PropertyTypes.FirstOrDefault(x => x.Alias == propertyAlias);
                if (routeProperty is null || !routeProperty.Mandatory)
                {
                    continue;
                }

                routeProperty.Mandatory = false;
                changed = true;
            }

            if (changed)
            {
                await contentTypeService.UpdateAsync(articulateContentType, Constants.Security.SuperUserKey);
            }
        }
    }
}
