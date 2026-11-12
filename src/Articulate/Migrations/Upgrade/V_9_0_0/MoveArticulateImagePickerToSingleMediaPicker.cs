#if UMBRACO_19_OR_GREATER
#nullable enable
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Articulate.Migrations.Upgrade.V_9_0_0;

/// <summary>
/// Moves the Articulate Image Picker onto Umbraco 19's single media picker so its properties yield <see cref="MediaWithCrops"/>.
/// </summary>
/// <remarks>
/// Upgraded sites get this from the CMS 19 migration, but fresh installs import package.zip after it has run.
/// The CMS step can't be reused here: on a fresh v19 site it also moves the core multiple pickers, which no longer store "multiple".
/// </remarks>
public sealed class MoveArticulateImagePickerToSingleMediaPicker(
    IMigrationContext context,
    IDataTypeService dataTypeService,
    PropertyEditorCollection propertyEditors,
    ILogger<MoveArticulateImagePickerToSingleMediaPicker> logger)
    : AsyncMigrationBase(context)
{
    /// <inheritdoc />
    protected override async Task MigrateAsync()
    {
        IDataType? dataType = await dataTypeService.GetAsync(ArticulateConstants.DataType.ArticulateImagePickerKey);
        if (dataType?.EditorAlias != Constants.PropertyEditors.Aliases.MediaPicker3)
        {
            return; // Already single (CMS upgrade) or removed.
        }

        if (!propertyEditors.TryGet(Constants.PropertyEditors.Aliases.SingleMediaPicker, out IDataEditor? editor))
        {
            logger.LogError("Could not move the Articulate Image Picker onto {EditorAlias}; the editor is not registered.", Constants.PropertyEditors.Aliases.SingleMediaPicker);
            return;
        }

        // Same changes as the CMS 19 migration: the single editor has no item count to configure or validate.
        _ = dataType.ConfigurationData.Remove("multiple");
        _ = dataType.ConfigurationData.Remove("validationLimit");
        dataType.Editor = editor;
        dataType.EditorUiAlias = "Umb.PropertyEditorUi.MediaPicker.Single";
        _ = await dataTypeService.UpdateAsync(dataType, Constants.Security.SuperUserKey);
        logger.LogInformation("Moved the Articulate Image Picker onto {EditorAlias}.", Constants.PropertyEditors.Aliases.SingleMediaPicker);
    }
}
#endif
