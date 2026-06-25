#nullable enable
using Articulate.Components;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;

namespace Articulate.Tests.Components
{
    [TestFixture]
    public class ArticulatePlanComponentTests
    {
        [TestCase(RuntimeLevel.Boot)]
        [TestCase(RuntimeLevel.Install)]
        [TestCase(RuntimeLevel.Upgrade)]
        public async Task InitializeAsync_does_not_execute_migrations_below_run(RuntimeLevel runtimeLevel)
        {
            Mock<IMigrationPlanExecutor> executor = new();
            ArticulatePlanComponent sut = new(
                Mock.Of<ICoreScopeProvider>(),
                executor.Object,
                Mock.Of<IKeyValueService>(),
                Mock.Of<IRuntimeState>(x => x.Level == runtimeLevel));

            await sut.InitializeAsync(false, CancellationToken.None);

            executor.VerifyNoOtherCalls();
        }

        [Test]
        public void InitializeAsync_honors_pre_cancelled_token()
        {
            ArticulatePlanComponent sut = new(
                Mock.Of<ICoreScopeProvider>(),
                Mock.Of<IMigrationPlanExecutor>(),
                Mock.Of<IKeyValueService>(),
                Mock.Of<IRuntimeState>(x => x.Level == RuntimeLevel.Run));
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await sut.InitializeAsync(false, cancellation.Token));
        }
    }
}
