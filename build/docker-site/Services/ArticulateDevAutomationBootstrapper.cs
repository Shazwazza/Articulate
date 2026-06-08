namespace ArticulateDockerSite.Services
{
    using ArticulateDockerSite.Options;
    using Microsoft.Extensions.Options;
    using Umbraco.Cms.Core;
    using Umbraco.Cms.Core.Configuration.Models;
    using Umbraco.Cms.Core.Events;
    using Umbraco.Cms.Core.Models;
    using Umbraco.Cms.Core.Models.Membership;
    using Umbraco.Cms.Core.Notifications;
    using Umbraco.Cms.Core.Security;
    using Umbraco.Cms.Core.Security.OperationStatus;
    using Umbraco.Cms.Core.Services;
    using Umbraco.Cms.Core.Services.OperationStatus;
    using Umbraco.Cms.Infrastructure.Security;

    /// <summary>
    /// Bootstraps a dev-only Articulate automation API user and client credentials at application startup.
    /// </summary>
    /// <remarks>
    /// The unattended install user is a regular backoffice account and cannot be used with the
    /// client_credentials grant required by the Umbraco Management API token endpoint. We therefore
    /// provision a separate <see cref="UserKind.Api"/> user and bind it to a client id/secret pair
    /// so smoke tests and dev automation can obtain a bearer token without manual backoffice setup.
    /// </remarks>
    internal sealed class ArticulateDevAutomationBootstrapper(
                IServiceScopeFactory scopeFactory,
                IOptions<ArticulateDevAutomationOptions> options,
                IOptions<RuntimeSettings> runtimeSettings,
                IRuntimeState runtimeState,
                IBackOfficeApplicationManager backOfficeApplicationManager,
                ILogger<ArticulateDevAutomationBootstrapper> logger) :
                INotificationAsyncHandler<UmbracoApplicationStartedNotification>
    {
        private const string ProductionSkipMessage =
                "Skipping Articulate dev automation bootstrap: production mode does not allow dev-only client provisioning.";

        /// <inheritdoc />
        public Task HandleAsync(
                UmbracoApplicationStartedNotification notification,
                CancellationToken cancellationToken) =>
                this.EnsureBootstrapAsync(cancellationToken);

        private async Task EnsureBootstrapAsync(CancellationToken cancellationToken)
        {
            ArticulateDevAutomationOptions settings = options.Value;

            if (runtimeSettings.Value.Mode == RuntimeMode.Production)
            {
                logger.LogWarning(ProductionSkipMessage);
                return;
            }

            if (runtimeState.Level == RuntimeLevel.Install)
            {
                logger.LogWarning("Skipping Articulate dev automation bootstrap: Umbraco installer is running.");
                return;
            }

            if (runtimeState.Level < RuntimeLevel.Run)
            {
                logger.LogWarning(
                    "Skipping Articulate dev automation bootstrap: runtime level '{Level}' is below Run.",
                    runtimeState.Level);
                return;
            }

            if (!ValidateOptions(settings))
            {
                return;
            }

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            IUserService userService = sp.GetRequiredService<IUserService>();
            IBackOfficeUserStore userStore = sp.GetRequiredService<IBackOfficeUserStore>();
            ICoreBackOfficeUserManager coreUserManager = sp.GetRequiredService<ICoreBackOfficeUserManager>();
            IBackOfficeUserClientCredentialsManager credentialsManager = sp.GetRequiredService<IBackOfficeUserClientCredentialsManager>();
            IUserGroupService userGroupService = sp.GetRequiredService<IUserGroupService>();

            IUser? clientBoundUser = await userService.FindByClientIdAsync(settings.ClientId);

            IReadOnlyList<IReadOnlyUserGroup> requiredGroups = await ResolveGroupsAsync(userGroupService, settings.UserGroupAliases, cancellationToken);
            if (requiredGroups.Count == 0)
            {
                return;
            }

            bool ensured = await ProvisionUserAsync(
                clientBoundUser,
                userStore,
                coreUserManager,
                credentialsManager,
                backOfficeApplicationManager,
                settings,
                requiredGroups,
                cancellationToken);

            if (ensured)
            {
                logger.LogInformation(
                    "Articulate dev automation bootstrap ensured API user '{Email}' with client '{ClientId}' and groups [{Groups}].",
                    settings.Email,
                    settings.ClientId,
                    string.Join(", ", settings.UserGroupAliases));
            }
        }

        private async Task<bool> ProvisionUserAsync(
            IUser? clientBoundUser,
            IBackOfficeUserStore userStore,
            ICoreBackOfficeUserManager coreUserManager,
            IBackOfficeUserClientCredentialsManager credentialsManager,
            IBackOfficeApplicationManager backOfficeAppManager,
            ArticulateDevAutomationOptions settings,
            IReadOnlyList<IReadOnlyUserGroup> requiredGroups,
            CancellationToken cancellationToken)
        {
            IUser? user = await EnsureUserExistsAsync(clientBoundUser, userStore, coreUserManager, settings, requiredGroups);
            if (user is null)
            {
                return false;
            }

            if (!await EnsureUserIsApiAndGroupsAsync(user, userStore, requiredGroups, settings))
            {
                return false;
            }

            if (!await EnsureCredentialsAsync(user, clientBoundUser, credentialsManager, backOfficeAppManager, settings, cancellationToken))
            {
                return false;
            }

            return true;
        }

        private async Task<IUser?> EnsureUserExistsAsync(
            IUser? clientBoundUser,
            IBackOfficeUserStore userStore,
            ICoreBackOfficeUserManager coreUserManager,
            ArticulateDevAutomationOptions settings,
            IReadOnlyList<IReadOnlyUserGroup> requiredGroups)
        {
            IUser? user = clientBoundUser ?? await userStore.GetByEmailAsync(settings.Email);
            if (user is null)
            {
                IdentityCreationResult? createResult = await CreateApiUserAsync(coreUserManager, settings, requiredGroups);
                if (createResult is null || !createResult.Succeded)
                {
                    logger.LogWarning(
                        "Articulate dev automation API user '{Email}' could not be created: {ErrorMessage}",
                        settings.Email,
                        createResult?.ErrorMessage ?? "unknown error");
                    return null;
                }

                user = await userStore.GetByEmailAsync(settings.Email);
                if (user is null)
                {
                    logger.LogWarning(
                        "Articulate dev automation API user '{Email}' was created but could not be reloaded.",
                        settings.Email);
                    return null;
                }
            }

            return user;
        }

        private async Task<bool> EnsureUserIsApiAndGroupsAsync(
            IUser user,
            IBackOfficeUserStore userStore,
            IReadOnlyList<IReadOnlyUserGroup> requiredGroups,
            ArticulateDevAutomationOptions settings)
        {
            if (user.Kind != UserKind.Api)
            {
                logger.LogWarning(
                    "Articulate dev automation bootstrap found an existing non-API user '{Email}'. Create an API user or change the bootstrap email.",
                    settings.Email);
                return false;
            }

            bool userChanged = EnsureRequiredGroups(user, requiredGroups);
            if (!user.IsApproved)
            {
                user.IsApproved = true;
                userChanged = true;
            }

            if (userChanged)
            {
                UserOperationStatus saveStatus = await userStore.SaveAsync(user);
                if (saveStatus != UserOperationStatus.Success)
                {
                    logger.LogWarning(
                        "Articulate dev automation bootstrap could not persist user '{Email}' group membership. Status: {Status}",
                        settings.Email,
                        saveStatus);
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> EnsureCredentialsAsync(
            IUser user,
            IUser? clientBoundUser,
            IBackOfficeUserClientCredentialsManager credentialsManager,
            IBackOfficeApplicationManager backOfficeAppManager,
            ArticulateDevAutomationOptions settings,
            CancellationToken cancellationToken)
        {
            if (clientBoundUser is null)
            {
                Attempt<BackOfficeUserClientCredentialsOperationStatus> credentialsResult =
                    await credentialsManager.SaveAsync(user.Key, settings.ClientId, settings.ClientSecret!);

                if (!credentialsResult.Success)
                {
                    logger.LogWarning(
                        "Articulate dev automation client credentials for '{ClientId}' could not be created. Status: {Status}",
                        settings.ClientId,
                        credentialsResult.Result);
                    return false;
                }
            }

            await backOfficeAppManager.EnsureBackOfficeClientCredentialsApplicationAsync(
                settings.ClientId,
                settings.ClientSecret!,
                cancellationToken);

            return true;
        }

        private bool ValidateOptions(ArticulateDevAutomationOptions settings)
        {
            if (!settings.Enabled)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.ClientId))
            {
                logger.LogWarning("Skipping Articulate dev automation bootstrap: no client id was configured.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.ClientSecret))
            {
                logger.LogWarning("Skipping Articulate dev automation bootstrap: no client secret was configured.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.Email))
            {
                logger.LogWarning("Skipping Articulate dev automation bootstrap: no API user email was configured.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.UserName))
            {
                logger.LogWarning("Skipping Articulate dev automation bootstrap: no API user username was configured.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.Name))
            {
                logger.LogWarning(
                    "Skipping Articulate dev automation bootstrap: no API user display name was configured.");
                return false;
            }

            return true;
        }

        private async Task<IdentityCreationResult?> CreateApiUserAsync(
                ICoreBackOfficeUserManager coreUserManager,
                ArticulateDevAutomationOptions settings,
                IReadOnlyList<IReadOnlyUserGroup> requiredGroups)
        {
            var createModel = new UserCreateModel
            {
                Email = settings.Email,
                UserName = settings.UserName,
                Name = settings.Name,
                Kind = UserKind.Api,
                UserGroupKeys = requiredGroups.Select(x => x.Key).ToHashSet()
            };

            IdentityCreationResult createResult = await coreUserManager.CreateAsync(createModel);
            return createResult.Succeded ? createResult : null;
        }

        private async Task<IReadOnlyList<IReadOnlyUserGroup>> ResolveGroupsAsync(
                IUserGroupService userGroupService,
                IEnumerable<string> aliases,
                CancellationToken cancellationToken)
        {
            var groups = new List<IReadOnlyUserGroup>();

            foreach (string alias in aliases.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                IUserGroup? group = await userGroupService.GetAsync(alias);
                if (group is null)
                {
                    logger.LogWarning(
                        "Skipping Articulate dev automation bootstrap: user group alias '{Alias}' could not be resolved.",
                        alias);
                    return Array.Empty<IReadOnlyUserGroup>();
                }

                groups.Add(group.ToReadOnlyGroup());
            }

            return groups;
        }

        private bool EnsureRequiredGroups(IUser user, IReadOnlyList<IReadOnlyUserGroup> requiredGroups)
        {
            bool changed = false;

            foreach (IReadOnlyUserGroup requiredGroup in requiredGroups)
            {
                if (user.Groups.Any(group => string.Equals(group.Alias, requiredGroup.Alias, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                user.AddGroup(requiredGroup);
                changed = true;
            }

            return changed;
        }
    }
}
