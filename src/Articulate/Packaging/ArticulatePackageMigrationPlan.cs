using System;
using Umbraco.Cms.Core.Packaging;

namespace Articulate.Packaging
{
    public class ArticulatePackageMigrationPlan : PackageMigrationPlan
    {
        public ArticulatePackageMigrationPlan() : base("Articulate")
        {
        }

        protected override void DefinePlan()
        {
            To<ArticulatePackageInstall>(new Guid("72A35073-9C56-4B69-873E-15E3536E9811"));
        }
    }
}
