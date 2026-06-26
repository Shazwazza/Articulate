using Umbraco.Cms.Infrastructure.Migrations;

namespace Articulate.Migrations.Upgrade;

/// <summary>
/// Represents the Articulate migration plan.
/// </summary>
/// <seealso cref="Umbraco.Cms.Infrastructure.Migrations.MigrationPlan" />
public sealed class ArticulatePlan : MigrationPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticulatePlan" /> class.
    /// </summary>
    public ArticulatePlan()
        : base(ArticulateConstants.Migration.ArticulatePackageMigrationPlan)
    {
        DefinePlan();
    }

    private void DefinePlan()
    {
        From(InitialState)
            .To<V_6_0_0.MigrateArticulateRichText>(new Guid("5B6B5B4C-F79A-4CC7-9D77-5F0326BD94FE"))
            .To<V_6_0_0.MakeBlogUrlNamesOptional>(new Guid("E8C8F7E4-B274-4CBB-9B7C-A5A2D2B03875"))
            .To<V_6_0_0.AddGiscusPerBlogProperties>(new Guid("7A4F3C2B-8E9D-4F1A-B2C5-6D7E8F9A0B1C"));
    }
}
