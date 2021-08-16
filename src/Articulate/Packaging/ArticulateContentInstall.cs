using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Packaging;

namespace Articulate.Packaging
{
    public class ArticulateContentInstall : PackageMigrationBase
    {
        private readonly ArticulateDataInstaller _articulateDataInstaller;

        public ArticulateContentInstall(
            ArticulateDataInstaller articulateDataInstaller,
            IPackagingService packagingService,
            IMediaService mediaService,
            MediaFileManager mediaFileManager,
            MediaUrlGeneratorCollection mediaUrlGenerators,
            IShortStringHelper shortStringHelper,
            IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
            IMigrationContext context)
            : base(packagingService, mediaService, mediaFileManager, mediaUrlGenerators, shortStringHelper, contentTypeBaseServiceProvider, context)
        {
            _articulateDataInstaller = articulateDataInstaller;
        }

        protected override void Migrate() => _articulateDataInstaller.InstallContent();
    }
}
