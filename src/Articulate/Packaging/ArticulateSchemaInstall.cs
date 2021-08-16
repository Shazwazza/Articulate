using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Packaging;

namespace Articulate.Packaging
{
    /// <summary>
    /// Installs the schema basd on the Umbraco package format.
    /// </summary>
    public class ArticulateSchemaInstall : PackageMigrationBase
    {
        public ArticulateSchemaInstall(IPackagingService packagingService, IMediaService mediaService, MediaFileManager mediaFileManager, MediaUrlGeneratorCollection mediaUrlGenerators, IShortStringHelper shortStringHelper, IContentTypeBaseServiceProvider contentTypeBaseServiceProvider, IMigrationContext context)
            : base(packagingService, mediaService, mediaFileManager, mediaUrlGenerators, shortStringHelper, contentTypeBaseServiceProvider, context)
        {
        }

        protected override void Migrate()
        {
            ImportPackage.FromEmbeddedResource<ArticulateSchemaInstall>().Do();
        }
    }
}
