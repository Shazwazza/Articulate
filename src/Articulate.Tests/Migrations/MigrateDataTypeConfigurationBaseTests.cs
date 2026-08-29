#nullable enable
using Articulate.Migrations.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Core.Serialization;

namespace Articulate.Tests.Migrations;

[TestFixture]
public class MigrateDataTypeConfigurationBaseTests
{
    [Test]
    public async Task UpdateDataTypeAsync_preserves_existing_configuration_when_editor_matches()
    {
        var id = Guid.NewGuid();
        var currentConfiguration = new Dictionary<string, object>
        {
            ["extensions"] = new[] { "Custom.Extension" },
            ["toolbar"] = new[] { new[] { "Custom.Toolbar" } },
        };
        DataType dataType = CreateDataType(currentConfiguration, "Umb.PropertyEditorUi.Tiptap");
        var dataTypeService = new Mock<IDataTypeService>();
        dataTypeService.Setup(x => x.GetAsync(id)).ReturnsAsync(dataType);
        Mock<IScopeProvider> scopeProvider = CreateScopeProvider();
        var migration = new TestMigration(
            Mock.Of<IMigrationContext>(),
            scopeProvider.Object,
            dataTypeService.Object);

        var result = await migration.RunUpdate(
            id,
            "Umb.PropertyEditorUi.Tiptap",
            "{\"extensions\":[\"Umb.Tiptap.Embed\"]}",
            "{\"extensions\":[\"Legacy.Extension\"]}");

        Assert.That(result, Is.Zero);
        Assert.That(dataType.ConfigurationData["extensions"], Is.EqualTo(new[] { "Custom.Extension" }));
        Assert.That(dataType.ConfigurationData["toolbar"], Is.EqualTo(new[] { new[] { "Custom.Toolbar" } }));
        dataTypeService.Verify(x => x.UpdateAsync(It.IsAny<IDataType>(), It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task UpdateDataTypeAsync_replaces_configuration_when_editor_changes()
    {
        var id = Guid.NewGuid();
        DataType dataType = CreateDataType(
            new Dictionary<string, object>
            {
                ["extensions"] = new[] { "Old.Extension" },
            },
            "Umb.PropertyEditorUi.TinyMce");
        var dataTypeService = new Mock<IDataTypeService>();
        dataTypeService.Setup(x => x.GetAsync(id)).ReturnsAsync(dataType);
        dataTypeService
            .Setup(x => x.UpdateAsync(It.IsAny<IDataType>(), It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<IDataType, DataTypeOperationStatus>.Succeed(DataTypeOperationStatus.Success, dataType));
        Mock<IScopeProvider> scopeProvider = CreateScopeProvider();
        var migration = new TestMigration(
            Mock.Of<IMigrationContext>(),
            scopeProvider.Object,
            dataTypeService.Object);

        var result = await migration.RunUpdate(
            id,
            "Umb.PropertyEditorUi.Tiptap",
            "{\"extensions\":[\"New.Extension\"]}");

        Assert.That(result, Is.EqualTo(1));
        Assert.That(dataType.EditorUiAlias, Is.EqualTo("Umb.PropertyEditorUi.Tiptap"));
        Assert.That(dataType.ConfigurationData["extensions"].ToString(), Does.Contain("New.Extension"));
        dataTypeService.Verify(x => x.UpdateAsync(dataType, It.IsAny<Guid>()), Times.Once);
    }

    [Test]
    public void UpdateDataTypeAsync_throws_when_update_fails()
    {
        var id = Guid.NewGuid();
        DataType dataType = CreateDataType(
            new Dictionary<string, object> { ["extensions"] = new[] { "Old.Extension" } },
            "Umb.PropertyEditorUi.TinyMce");
        var dataTypeService = new Mock<IDataTypeService>();
        dataTypeService.Setup(x => x.GetAsync(id)).ReturnsAsync(dataType);
        dataTypeService
            .Setup(x => x.UpdateAsync(It.IsAny<IDataType>(), It.IsAny<Guid>()))
            .ReturnsAsync(default(Attempt<IDataType, DataTypeOperationStatus>));
        Mock<IScopeProvider> scopeProvider = CreateScopeProvider();
        var migration = new TestMigration(
            Mock.Of<IMigrationContext>(),
            scopeProvider.Object,
            dataTypeService.Object);

        Assert.That(
            async () => await migration.RunUpdate(
                id,
                "Umb.PropertyEditorUi.Tiptap",
                "{\"extensions\":[\"New.Extension\"]}"),
            Throws.TypeOf<InvalidOperationException>());
    }

    private static Mock<IScopeProvider> CreateScopeProvider()
    {
        var scopeProvider = new Mock<IScopeProvider>();
        scopeProvider
            .Setup(x => x.CreateScope(
                It.IsAny<IsolationLevel>(),
                It.IsAny<Umbraco.Cms.Core.Scoping.RepositoryCacheMode>(),
                It.IsAny<IEventDispatcher>(),
                It.IsAny<IScopedNotificationPublisher>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                true))
            .Returns(Mock.Of<IScope>());
        return scopeProvider;
    }

    private static DataType CreateDataType(IDictionary<string, object> configuration, string editorUiAlias)
    {
        var configurationEditor = new Mock<IConfigurationEditor>();
        configurationEditor.SetupGet(x => x.DefaultConfiguration).Returns(new Dictionary<string, object>());
        var editor = new Mock<IDataEditor>();
        editor.SetupGet(x => x.Alias).Returns("Umbraco.RichText");
        editor.Setup(x => x.GetConfigurationEditor()).Returns(configurationEditor.Object);
        return new DataType(editor.Object, Mock.Of<IConfigurationEditorJsonSerializer>())
        {
            EditorUiAlias = editorUiAlias,
            ConfigurationData = configuration,
        };
    }

    private sealed class TestMigration(
        IMigrationContext context,
        IScopeProvider scopeProvider,
        IDataTypeService dataTypeService)
        : MigrateDataTypeConfigurationBase(
            context,
            scopeProvider,
            dataTypeService,
            NullLogger<MigrateDataTypeConfigurationBase>.Instance)
    {
        public Task<int> RunUpdate(
            Guid id,
            string editorUiAlias,
            string configurationJson,
            string? expectedCurrentConfigurationJson = null) =>
            UpdateDataTypeAsync(id, editorUiAlias, configurationJson, expectedCurrentConfigurationJson);

        protected override Task MigrateAsync() => Task.CompletedTask;
    }
}
