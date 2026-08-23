#nullable enable
using Microsoft.Extensions.Logging;
using Articulate.Migrations.Upgrade.V_6_0_0;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Articulate.Migrations.Upgrade.V_7_0_0;

/// <summary>
/// Applies the complete Tiptap configuration to existing Articulate Rich Text data types.
/// </summary>
public sealed class MigrateArticulateRichTextTiptapConfiguration(
    IMigrationContext context,
    IScopeProvider scopeProvider,
    IDataTypeService dataTypeService,
    ILogger<MigrateArticulateRichTextTiptapConfiguration> logger)
    : MigrateDataTypeConfigurationBase(context, scopeProvider, dataTypeService, logger)
{
    private const string TiptapEditorUiAlias = "Umb.PropertyEditorUi.Tiptap";
    private const string TiptapConfigurationJson = "{\"extensions\":[\"Umb.Tiptap.Embed\",\"Umb.Tiptap.Link\",\"Umb.Tiptap.Figure\",\"Umb.Tiptap.Image\",\"Umb.Tiptap.Table\",\"Umb.Tiptap.MediaUpload\",\"Umb.Tiptap.Anchor\",\"Umb.Tiptap.CodeBlock\",\"Umb.Tiptap.Bold\",\"Umb.Tiptap.Italic\",\"Umb.Tiptap.Strike\",\"Umb.Tiptap.Underline\",\"Umb.Tiptap.TextAlign\",\"Umb.Tiptap.BulletList\",\"Umb.Tiptap.OrderedList\",\"Umb.Tiptap.Heading\",\"Umb.Tiptap.HorizontalRule\",\"Umb.Tiptap.Blockquote\",\"Umb.Tiptap.HtmlAttributeClass\",\"Umb.Tiptap.HtmlAttributeDataset\",\"Umb.Tiptap.HtmlAttributeId\",\"Umb.Tiptap.HtmlAttributeStyle\",\"Umb.Tiptap.HtmlTagDiv\",\"Umb.Tiptap.HtmlTagSpan\",\"Umb.Tiptap.Subscript\",\"Umb.Tiptap.Superscript\",\"Umb.Tiptap.TextDirection\",\"Umb.Tiptap.TextIndent\"],\"maxImageSize\":500,\"overlaySize\":\"medium\",\"toolbar\":[[[\"Umb.Tiptap.Toolbar.SourceEditor\"],[\"Umb.Tiptap.Toolbar.Bold\",\"Umb.Tiptap.Toolbar.Italic\",\"Umb.Tiptap.Toolbar.Underline\"],[\"Umb.Tiptap.Toolbar.TextAlignLeft\",\"Umb.Tiptap.Toolbar.TextAlignCenter\",\"Umb.Tiptap.Toolbar.TextAlignRight\"],[\"Umb.Tiptap.Toolbar.BulletList\",\"Umb.Tiptap.Toolbar.OrderedList\"],[\"Umb.Tiptap.Toolbar.Blockquote\",\"Umb.Tiptap.Toolbar.HorizontalRule\"],[\"Umb.Tiptap.Toolbar.Link\",\"Umb.Tiptap.Toolbar.Unlink\"],[\"Umb.Tiptap.Toolbar.MediaPicker\",\"Umb.Tiptap.Toolbar.EmbeddedMedia\"]]],\"allowedMediaTypes\":\"cc07b313-0843-4aa8-bbda-871c8da728c8\"}";

    /// <inheritdoc />
    protected override async Task MigrateAsync()
    {
        if (MigrateArticulateRichText.IsTinyMcePackageInstalled())
        {
            logger.LogInformation(
                "Skipping Articulate Rich Text Tiptap configuration migration because TinyMCE.Umbraco is installed.");
            return;
        }

        int updated = await UpdateDataTypeAsync(
            ArticulateConstants.DataType.ArticulateRichTextKey,
            TiptapEditorUiAlias,
            TiptapConfigurationJson);

        logger.LogInformation("Updated {Count} Articulate Rich Text data type records.", updated);
    }
}
