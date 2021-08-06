using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Packaging;

namespace Articulate.Packaging
{
    public class ArticulatePackageInstall : PackageMigrationBase
    {
        public ArticulatePackageInstall(IPackagingService packagingService, IMigrationContext context) : base(packagingService, context)
        {
        }

        protected override void Migrate()
        {
            ImportPackage.FromEmbeddedResource<ArticulatePackageInstall>().Do();
        }
    }
}
