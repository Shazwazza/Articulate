#nullable enable
using Articulate.Options;
using Articulate.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using OpenIddict.Abstractions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Services;

namespace Articulate.Tests.Services
{
    [TestFixture]
    public class ArticulateApplicationManagerTests
    {
        [Test]
        public async Task HandleAsync_does_not_log_development_warning_when_override_is_disabled()
        {
            var logger = new RecordingLogger<ArticulateApplicationManager>();
            ArticulateApplicationManager sut = CreateSut(
                logger,
                articulateOptions: new ArticulateOptions
                {
                    AllowUnsafeLocalExternalImageHostsInDevelopment = false
                });

            await sut.HandleAsync(null!, CancellationToken.None);

            Assert.That(logger.Messages, Does.Not.Contain(
                "Articulate development-only local external image host importing is enabled. Loopback and private-network targets may be fetched when their hosts are listed in Articulate:AllowedMediaHosts."));
        }

        [Test]
        public async Task HandleAsync_logs_development_warning_when_override_is_enabled_outside_production()
        {
            var logger = new RecordingLogger<ArticulateApplicationManager>();
            ArticulateApplicationManager sut = CreateSut(
                logger,
                runtimeMode: RuntimeMode.BackofficeDevelopment,
                articulateOptions: new ArticulateOptions
                {
                    AllowUnsafeLocalExternalImageHostsInDevelopment = true
                });

            await sut.HandleAsync(null!, CancellationToken.None);

            Assert.That(logger.Messages, Does.Contain(
                "Articulate development-only local external image host importing is enabled. Loopback and private-network targets may be fetched when their hosts are listed in Articulate:AllowedMediaHosts."));
        }

        [Test]
        public async Task HandleAsync_does_not_log_development_warning_when_override_is_enabled_in_production()
        {
            var logger = new RecordingLogger<ArticulateApplicationManager>();
            ArticulateApplicationManager sut = CreateSut(
                logger,
                runtimeMode: RuntimeMode.Production,
                articulateOptions: new ArticulateOptions
                {
                    AllowUnsafeLocalExternalImageHostsInDevelopment = true
                });

            await sut.HandleAsync(null!, CancellationToken.None);

            Assert.That(logger.Messages, Does.Not.Contain(
                "Articulate development-only local external image host importing is enabled. Loopback and private-network targets may be fetched when their hosts are listed in Articulate:AllowedMediaHosts."));
        }

        [Test]
        public async Task HandleAsync_creates_enabled_open_id_client_with_configured_descriptor()
        {
            var applicationManager = new Mock<IOpenIddictApplicationManager>();
            OpenIddictApplicationDescriptor? descriptor = null;
            applicationManager
                .Setup(x => x.FindByClientIdAsync("articulate-editor", It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<object?>((object?)null));
            applicationManager
                .Setup(x => x.CreateAsync(
                    It.IsAny<OpenIddictApplicationDescriptor>(),
                    It.IsAny<CancellationToken>()))
                .Callback<OpenIddictApplicationDescriptor, CancellationToken>((value, _) => descriptor = value)
                .Returns(new ValueTask<object>(new object()));

            using ServiceProvider serviceProvider = CreateServiceProvider(applicationManager.Object);
            ArticulateApplicationManager sut = CreateSut(
                new RecordingLogger<ArticulateApplicationManager>(),
                scopeFactory: serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                openIdClientOptions: new ArticulateOpenIdClientOptions
                {
                    Enabled = true,
                    ClientId = "articulate-editor",
                    DisplayName = "Articulate Editor",
                    RedirectUris = ["https://example.com/editor/callback"],
                    PostLogoutRedirectUris = ["https://example.com/editor/logout"]
                });

            await sut.HandleAsync(null!, CancellationToken.None);

            applicationManager.Verify(
                x => x.CreateAsync(
                    It.IsAny<OpenIddictApplicationDescriptor>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor!.ClientId, Is.EqualTo("articulate-editor"));
            Assert.That(descriptor.DisplayName, Is.EqualTo("Articulate Editor"));
            Assert.That(descriptor.ClientType, Is.EqualTo(OpenIddictConstants.ClientTypes.Public));
            Assert.That(descriptor.RedirectUris, Does.Contain(new Uri("https://example.com/editor/callback/")));
            Assert.That(
                descriptor.PostLogoutRedirectUris,
                Does.Contain(new Uri("https://example.com/editor/logout/")));
            Assert.That(
                descriptor.Permissions,
                Is.EquivalentTo(new ArticulateOpenIdClientOptions().Permissions));
            Assert.That(
                descriptor.Requirements,
                Is.EquivalentTo(new ArticulateOpenIdClientOptions().Requirements));
        }

        [Test]
        public async Task HandleAsync_updates_existing_open_id_client()
        {
            var applicationManager = new Mock<IOpenIddictApplicationManager>();
            object existingApplication = new();
            OpenIddictApplicationDescriptor? descriptor = null;
            applicationManager
                .Setup(x => x.FindByClientIdAsync("articulate-editor", It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<object?>(existingApplication));
            applicationManager
                .Setup(x => x.UpdateAsync(
                    existingApplication,
                    It.IsAny<OpenIddictApplicationDescriptor>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, OpenIddictApplicationDescriptor, CancellationToken>((_, value, _) => descriptor = value)
                .Returns(ValueTask.CompletedTask);

            using ServiceProvider serviceProvider = CreateServiceProvider(applicationManager.Object);
            ArticulateApplicationManager sut = CreateSut(
                new RecordingLogger<ArticulateApplicationManager>(),
                scopeFactory: serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                openIdClientOptions: new ArticulateOpenIdClientOptions
                {
                    Enabled = true,
                    ClientId = "articulate-editor",
                    RedirectUris = ["https://example.com/editor/callback"]
                });

            await sut.HandleAsync(null!, CancellationToken.None);

            applicationManager.Verify(
                x => x.UpdateAsync(
                    existingApplication,
                    It.IsAny<OpenIddictApplicationDescriptor>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            applicationManager.Verify(
                x => x.CreateAsync(
                    It.IsAny<OpenIddictApplicationDescriptor>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor!.ClientId, Is.EqualTo("articulate-editor"));
            Assert.That(descriptor.DisplayName, Is.EqualTo("articulate-editor"));
            Assert.That(descriptor.ClientType, Is.EqualTo(OpenIddictConstants.ClientTypes.Public));
            Assert.That(descriptor.RedirectUris, Does.Contain(new Uri("https://example.com/editor/callback/")));
            Assert.That(descriptor.PostLogoutRedirectUris, Is.Empty);
            Assert.That(
                descriptor.Permissions,
                Is.EquivalentTo(new ArticulateOpenIdClientOptions().Permissions));
            Assert.That(
                descriptor.Requirements,
                Is.EquivalentTo(new ArticulateOpenIdClientOptions().Requirements));
        }

        [Test]
        public async Task HandleAsync_ignores_invalid_redirect_uris_when_a_valid_uri_exists()
        {
            var applicationManager = new Mock<IOpenIddictApplicationManager>();
            OpenIddictApplicationDescriptor? descriptor = null;
            applicationManager
                .Setup(x => x.FindByClientIdAsync("articulate-editor", It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<object?>((object?)null));
            applicationManager
                .Setup(x => x.CreateAsync(
                    It.IsAny<OpenIddictApplicationDescriptor>(),
                    It.IsAny<CancellationToken>()))
                .Callback<OpenIddictApplicationDescriptor, CancellationToken>((value, _) => descriptor = value)
                .Returns(new ValueTask<object>(new object()));
            var logger = new RecordingLogger<ArticulateApplicationManager>();

            using ServiceProvider serviceProvider = CreateServiceProvider(applicationManager.Object);
            ArticulateApplicationManager sut = CreateSut(
                logger,
                scopeFactory: serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                openIdClientOptions: new ArticulateOpenIdClientOptions
                {
                    Enabled = true,
                    ClientId = "articulate-editor",
                    RedirectUris = ["not a URI", "https://example.com/editor/callback"]
                });

            await sut.HandleAsync(null!, CancellationToken.None);

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor!.RedirectUris, Has.Count.EqualTo(1));
            Assert.That(descriptor.RedirectUris, Does.Contain(new Uri("https://example.com/editor/callback/")));
            Assert.That(
                string.Join(Environment.NewLine, logger.Messages),
                Does.Contain("Ignoring invalid redirect URI 'not a URI'"));
        }

        private static ArticulateApplicationManager CreateSut(
            RecordingLogger<ArticulateApplicationManager> logger,
            IServiceScopeFactory? scopeFactory = null,
            ArticulateOpenIdClientOptions? openIdClientOptions = null,
            RuntimeMode runtimeMode = RuntimeMode.BackofficeDevelopment,
            ArticulateOptions? articulateOptions = null)
        {
            return new ArticulateApplicationManager(
                scopeFactory ?? Mock.Of<IServiceScopeFactory>(),
                Microsoft.Extensions.Options.Options.Create(openIdClientOptions ?? new ArticulateOpenIdClientOptions
                {
                    Enabled = false
                }),
                Microsoft.Extensions.Options.Options.Create(articulateOptions ?? new ArticulateOptions()),
                Microsoft.Extensions.Options.Options.Create(new RuntimeSettings { Mode = runtimeMode }),
                Mock.Of<IRuntimeState>(runtimeState => runtimeState.Level == RuntimeLevel.Run),
                logger);
        }

        private static ServiceProvider CreateServiceProvider(IOpenIddictApplicationManager applicationManager) =>
            new ServiceCollection()
                .AddSingleton(applicationManager)
                .BuildServiceProvider();

        private sealed class RecordingLogger<T> : ILogger<T>
        {
            private readonly List<string> _messages = [];

            public IReadOnlyList<string> Messages => _messages;

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull =>
                NoopDisposable.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _messages.Add(formatter(state, exception));
            }

            private sealed class NoopDisposable : IDisposable
            {
                public static readonly NoopDisposable Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
