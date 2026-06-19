#nullable enable
using Articulate.Options;
using Articulate.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
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
                articulateOptions: new ArticulateOptions { AllowUnsafeLocalExternalImageHostsInDevelopment = false });

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
                RuntimeMode.BackofficeDevelopment,
                new ArticulateOptions { AllowUnsafeLocalExternalImageHostsInDevelopment = true });

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
                RuntimeMode.Production,
                new ArticulateOptions { AllowUnsafeLocalExternalImageHostsInDevelopment = true });

            await sut.HandleAsync(null!, CancellationToken.None);

            Assert.That(logger.Messages, Does.Not.Contain(
                "Articulate development-only local external image host importing is enabled. Loopback and private-network targets may be fetched when their hosts are listed in Articulate:AllowedMediaHosts."));
        }

        private static ArticulateApplicationManager CreateSut(
            RecordingLogger<ArticulateApplicationManager> logger,
            RuntimeMode runtimeMode = RuntimeMode.BackofficeDevelopment,
            ArticulateOptions? articulateOptions = null) =>
            new(
                Mock.Of<IServiceScopeFactory>(),
                Microsoft.Extensions.Options.Options.Create(new ArticulateOpenIdClientOptions { Enabled = false }),
                Microsoft.Extensions.Options.Options.Create(articulateOptions ?? new ArticulateOptions()),
                Microsoft.Extensions.Options.Options.Create(new RuntimeSettings { Mode = runtimeMode }),
                Mock.Of<IRuntimeState>(runtimeState => runtimeState.Level == RuntimeLevel.Run),
                logger);

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
                Func<TState, Exception?, string> formatter) =>
                _messages.Add(formatter(state, exception));

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
