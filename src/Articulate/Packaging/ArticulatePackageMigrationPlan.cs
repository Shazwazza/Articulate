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
            To<ArticulateSchemaInstall>(new Guid("72A35073-9C56-4B69-873E-15E3536E9811"));
            To<ArticulateContentInstall>(new Guid("D1DE9B8E-4EBC-47D3-9C06-D6584E8A441B"));

        }
    }
}
