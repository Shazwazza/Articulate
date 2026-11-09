#nullable enable
using Articulate.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Articulate.Services
{
    /// <summary>
    /// Ensures the Articulate-specific OpenIddict client is registered at application startup.
    /// </summary>
    internal sealed class ArticulateApplicationManager(
        IServiceScopeFactory scopeFactory,
        IOptions<ArticulateOpenIdClientOptions> options,
        IOptions<ArticulateOptions> articulateOptions,
        IOptions<RuntimeSettings> runtimeSettings,
        IRuntimeState runtimeState,
        ILogger<ArticulateApplicationManager> logger) :
        INotificationAsyncHandler<UmbracoApplicationStartedNotification>
    {
        /// <inheritdoc />
        public Task HandleAsync(
            UmbracoApplicationStartedNotification notification,
            CancellationToken cancellationToken) =>
            EnsureOpenIdClientAsync(cancellationToken);

        private async Task EnsureOpenIdClientAsync(CancellationToken cancellationToken)
        {
            ArticulateOpenIdClientOptions settings = options.Value;
            ArticulateOptions articulateSettings = articulateOptions.Value;
            bool isProductionMode = runtimeSettings.Value.Mode == RuntimeMode.Production;
            bool allowUnsafeLocalExternalImageHosts =
                !isProductionMode &&
                articulateSettings.AllowUnsafeLocalExternalImageHostsInDevelopment;

            if (runtimeState.Level >= RuntimeLevel.Run &&
                allowUnsafeLocalExternalImageHosts)
            {
                logger.LogWarning(
                    "Articulate development-only local external image host importing is enabled. Loopback and private-network targets may be fetched when their hosts are listed in Articulate:AllowedMediaHosts.");
            }

            if (runtimeState.Level == RuntimeLevel.Install)
            {
                logger.LogWarning("Skipping Articulate OpenId client registration: Umbraco installer is running.");
                return;
            }

            if (runtimeState.Level < RuntimeLevel.Run)
            {
                logger.LogWarning(
                    "Skipping Articulate OpenId client registration: runtime level '{Level}' is below Run.",
                    runtimeState.Level);
                return;
            }

            if (!settings.Enabled)
            {
                logger.LogWarning("Articulate OpenId client registration disabled via configuration.");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.ClientId))
            {
                logger.LogWarning(
                    "Articulate OpenId client registration skipped because no client id was configured.");
                return;
            }

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IOpenIddictApplicationManager applications =
                scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            OpenIddictApplicationDescriptor? descriptor = BuildDescriptor(settings);
            if (descriptor is null)
            {
                logger.LogWarning(
                    "Articulate OpenId client '{ClientId}' was not created. Provide at least one valid redirect URI.",
                    settings.ClientId);
                return;
            }

            object? existing = await applications.FindByClientIdAsync(settings.ClientId, cancellationToken);

            if (existing is null)
            {
                _ = await applications.CreateAsync(descriptor, cancellationToken);
                logger.LogInformation("Registered OpenIddict client '{ClientId}' for Articulate.", settings.ClientId);
            }
            else
            {
                await applications.UpdateAsync(existing, descriptor, cancellationToken);
                logger.LogInformation("Updated OpenIddict client '{ClientId}' for Articulate.", settings.ClientId);
            }
        }

        private OpenIddictApplicationDescriptor? BuildDescriptor(ArticulateOpenIdClientOptions settings)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = settings.ClientId,
                DisplayName =
                    string.IsNullOrWhiteSpace(settings.DisplayName) ? settings.ClientId : settings.DisplayName,
                ClientType = settings.ClientType
            };

            if (settings.HasClientSecret())
            {
                descriptor.ClientSecret = settings.ClientSecret;
            }

            if (!TryPopulateMandatoryUris(
                    descriptor.RedirectUris,
                    settings.RedirectUris,
                    settings.ClientId ?? string.Empty))
            {
                return null;
            }

            TryPopulateOptionalUris(
                descriptor.PostLogoutRedirectUris,
                settings.PostLogoutRedirectUris,
                settings.ClientId ?? string.Empty);

            if (settings.Permissions.Count > 0)
            {
                descriptor.Permissions.UnionWith(settings.Permissions);
            }

            if (settings.Requirements.Count > 0)
            {
                descriptor.Requirements.UnionWith(settings.Requirements);
            }

            return descriptor;
        }

        private bool TryPopulateMandatoryUris(ICollection<Uri> target, IEnumerable<string> sources, string clientId)
        {
            bool added = false;

            foreach (string candidate in sources)
            {
                if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
                {
                    target.Add(NormalizeUri(uri));
                    added = true;
                }
                else
                {
                    logger.LogWarning(
                        "Ignoring invalid redirect URI '{Uri}' while registering OpenIddict client '{ClientId}'.",
                        candidate,
                        clientId);
                }
            }

            return added;
        }

        // OpenIddict persists Uri.OriginalString and compares redirect URIs ordinally. UriBuilder.Uri makes
        // default ports explicit (e.g. https://host:443/), which never matches the portless URI sent by browsers.
        private static Uri NormalizeUri(Uri uri)
        {
            var builder = new UriBuilder(uri) { Path = uri.AbsolutePath.EnsureEndsWith('/') };
            return new Uri(builder.Uri.AbsoluteUri);
        }

        private void TryPopulateOptionalUris(ICollection<Uri> target, IEnumerable<string> sources, string clientId)
        {
            foreach (string candidate in sources)
            {
                if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
                {
                    target.Add(NormalizeUri(uri));
                }
                else
                {
                    logger.LogWarning(
                        "Ignoring invalid post-logout redirect URI '{Uri}' while registering OpenIddict client '{ClientId}'.",
                        candidate,
                        clientId);
                }
            }
        }
    }
}
