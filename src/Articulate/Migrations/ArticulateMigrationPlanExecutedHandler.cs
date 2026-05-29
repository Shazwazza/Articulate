using Articulate.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Notifications;
using Umbraco.Cms.Infrastructure.Scoping;

#nullable enable

namespace Articulate.Migrations;

/// <summary>
/// Handles notifications for Articulate migration plans.
/// </summary>
internal class ArticulateMigrationPlanExecutedHandler(
    IRuntimeState runtimeState,
    IContentService contentService,
    IContentTypeService contentTypeService,
    ISqlContext sqlContext,
    ILogger<ArticulateMigrationPlanExecutedHandler> logger,
    IOptions<ArticulateOptions> options,
    IScopeProvider scopeProvider)
    : INotificationHandler<MigrationPlansExecutedNotification>, INotificationHandler<ImportedPackageNotification>
{
    /// <summary>
    /// Handles the <see cref="MigrationPlansExecutedNotification"/>.
    /// </summary>
    /// <param name="notification">The notification information.</param>
    public void Handle(MigrationPlansExecutedNotification notification)
    {
        if (!ShouldPublishAfterMigration("migration execution", notification.ExecutedPlans))
        {
            return;
        }

        PublishArticulateRoots(
            "migration execution",
            GetAllArticulateRoots(),
            allowPublishingUnpublishedRoots: new HashSet<int>());
    }

    /// <summary>
    /// Handles the <see cref="ImportedPackageNotification"/>.
    /// </summary>
    /// <param name="notification">The notification information.</param>
    public void Handle(ImportedPackageNotification notification)
    {
        IReadOnlyList<IContent> installedArticulateRoots =
            GetInstalledArticulateRoots(notification.InstallationSummary);
        LogPackageImportContentSummary(notification.InstallationSummary, installedArticulateRoots);
        if (!ShouldPublishAfterPackageImport("package import", notification.InstallationSummary, installedArticulateRoots))
        {
            return;
        }

        HashSet<int> installedRootIds = [.. installedArticulateRoots.Select(x => x.Id)];
        PublishArticulateRoots(
            "package import",
            installedArticulateRoots,
            installedRootIds);
    }

    private bool ShouldPublishAfterMigration(string trigger, IEnumerable<ExecutedMigrationPlan> executedPlans)
    {
        if (!CanAutoPublish(trigger))
        {
            return false;
        }

        if (!HasMigrationRun(executedPlans))
        {
            logger.LogInformation(
                "No Articulate migrations have run for this notification ({Trigger}), skipping publish.", trigger);
            return false;
        }

        return true;
    }

    private bool ShouldPublishAfterPackageImport(
        string trigger,
        InstallationSummary installationSummary,
        IReadOnlyList<IContent> installedArticulateRoots)
    {
        if (!CanAutoPublish(trigger))
        {
            return false;
        }

        if (installedArticulateRoots.Count == 0)
        {
            logger.LogInformation(
                "Package '{PackageName}' did not install any Articulate roots, skipping publish for {Trigger}.",
                installationSummary.PackageName,
                trigger);
            return false;
        }

        return true;
    }

    private bool CanAutoPublish(string trigger)
    {
        logger.LogDebug(
            "Articulate CanAutoPublish check for {Trigger}: RuntimeLevel={RuntimeLevel}, AutoPublishOnStartup={AutoPublishOnStartup}",
            trigger,
            runtimeState.Level,
            options.Value.AutoPublishOnStartup);

        // Only Run, Install or Upgrade are valid runtime levels to consider migration/publish tasks.
        // Any other level (Boot, BootFailed, Unknown, etc.) is not a migration scenario and will be skipped.
        if (runtimeState.Level is not (RuntimeLevel.Run or RuntimeLevel.Install or RuntimeLevel.Upgrade))
        {
            logger.LogInformation(
                "Umbraco runtime level {RuntimeLevel} is not valid for migration; skipping Articulate post-migration tasks ({Trigger}).",
                runtimeState.Level,
                trigger);
            return false;
        }

        if (!options.Value.AutoPublishOnStartup)
        {
            logger.LogInformation(
                "AutoPublishOnStartup is false, skipping Articulate post-migration tasks ({Trigger}).", trigger);
            return false;
        }

        logger.LogInformation(
            "Articulate post-migration tasks will proceed for {Trigger} (RuntimeLevel={RuntimeLevel}).",
            trigger,
            runtimeState.Level);

        return true;
    }

    private void PublishArticulateRoots(
        string trigger,
        IEnumerable<IContent> articulateRoots,
        IReadOnlySet<int> allowPublishingUnpublishedRoots)
    {
        logger.LogInformation("Beginning Articulate post-migration tasks triggered by {Trigger}", trigger);

        var attemptedAny = false;
        foreach (IContent contentHome in articulateRoots.GroupBy(x => x.Id).Select(x => x.First()))
        {
            if (!TryGetPublishBranchFilter(contentHome, allowPublishingUnpublishedRoots, out PublishBranchFilter filter))
            {
                logger.LogInformation(
                    "Skipping unpublished Articulate root node ID {NodeId} for trigger {Trigger} because it is not eligible for auto-publish in this context.",
                    contentHome.Id,
                    trigger);
                continue;
            }

            logger.LogInformation(
                "Found Articulate root node with ID {NodeId} for trigger {Trigger}",
                contentHome.Id,
                trigger);

            try
            {
                logger.LogInformation(
                    "Attempting to publish Articulate branch for root node ID {NodeId} and trigger {Trigger}",
                    contentHome.Id,
                    trigger);

                logger.LogInformation(
                    "Publish filter for root node ID {NodeId} is {Filter} (Published: {IsPublished})",
                    contentHome.Id,
                    filter,
                    contentHome.Published);

                using IScope scope = scopeProvider.CreateScope(autoComplete: true);
                IEnumerable<PublishResult> resultEnumerable =
                    contentService.PublishBranch(contentHome, filter, []);
                var result = resultEnumerable.ToList();
                attemptedAny = true;

                if (result.All(r => r.Success))
                {
                    logger.LogInformation(
                        "Published Articulate Home page and descendants for root node ID {NodeId} after {Trigger}",
                        contentHome.Id,
                        trigger);
                }
                else
                {
                    var failures = result.Where(r => !r.Success).ToList();
                    logger.LogWarning(
                        "Partial publish failure for root node ID {NodeId}: {FailureCount} items failed for trigger {Trigger}",
                        contentHome.Id,
                        failures.Count,
                        trigger);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error publishing Articulate Home page and descendants for root node ID {NodeId} after {Trigger}",
                    contentHome.Id,
                    trigger);
            }
        }

        if (!attemptedAny)
        {
            logger.LogInformation("No Articulate root nodes were eligible for publish when handling {Trigger}", trigger);
        }
    }

    private IEnumerable<IContent> GetAllArticulateRoots()
    {
        IContentType? articulateContentType = contentTypeService.Get(ArticulateConstants.ContentType.Articulate);
        if (articulateContentType is null)
        {
            logger.LogWarning("The Articulate content type was not found when attempting to locate Articulate roots.");
            return [];
        }

        return contentService.GetPagedOfType(
                articulateContentType.Id,
                0,
                int.MaxValue,
                out _,
                sqlContext.Query<IContent>().Where(x => x.Trashed == false))
            .Where(x => !x.Trashed)
            .ToArray();
    }

    private static IReadOnlyList<IContent> GetInstalledArticulateRoots(InstallationSummary installationSummary) =>
        installationSummary.ContentInstalled
            .Where(x => x.ContentType.Alias == ArticulateConstants.ContentType.Articulate && !x.Trashed)
            .ToArray();

    private void LogPackageImportContentSummary(
        InstallationSummary installationSummary,
        IReadOnlyList<IContent> installedArticulateRoots)
    {
        IContent[] installedContent = [.. installationSummary.ContentInstalled];
        string[] installedAliases = installedContent
            .Select(x => x.ContentType.Alias)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logger.LogInformation(
            "Package '{PackageName}' installed {InstalledContentCount} content items with aliases [{InstalledContentAliases}] and {InstalledArticulateRootCount} Articulate roots.",
            installationSummary.PackageName,
            installedContent.Length,
            string.Join(", ", installedAliases),
            installedArticulateRoots.Count);
    }

    private static bool TryGetPublishBranchFilter(
        IContent contentHome,
        IReadOnlySet<int> allowPublishingUnpublishedRoots,
        out PublishBranchFilter filter)
    {
        if (allowPublishingUnpublishedRoots.Contains(contentHome.Id))
        {
            filter = PublishBranchFilter.All;
            return true;
        }

        if (contentHome.Published)
        {
            filter = PublishBranchFilter.ForceRepublish;
            return true;
        }

        filter = default;
        return false;
    }

    private bool HasMigrationRun(IEnumerable<ExecutedMigrationPlan> executedMigrationPlans)
    {
        foreach (ExecutedMigrationPlan executedMigrationPlan in executedMigrationPlans)
        {
            logger.LogInformation(
                "Executed {Name}:{Success}",
                executedMigrationPlan.Plan.Name,
                executedMigrationPlan.Successful);

            if (executedMigrationPlan.Successful &&
                (executedMigrationPlan.Plan.Name == ArticulateConstants.Migration.ArticulatePackageMigrationPlan ||
                 executedMigrationPlan.Plan.Name == ArticulateConstants.Migration.AutomaticPackageMigrationPlan))
            {
                return true;
            }
        }

        return false;
    }
}
