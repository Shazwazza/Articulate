#nullable enable
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Articulate.Migrations.Upgrade
{
    /// <summary>
    /// Base class for migrations that update data type configurations.
    /// </summary>
    public abstract class MigrateDataTypeConfigurationBase : AsyncMigrationBase
    {
        private readonly IScopeProvider _scopeProvider;
        private readonly IDataTypeService _dataTypeService;
        private readonly ILogger<MigrateDataTypeConfigurationBase> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MigrateDataTypeConfigurationBase"/> class.
        /// </summary>
        protected MigrateDataTypeConfigurationBase(
            IMigrationContext context,
            IScopeProvider scopeProvider,
            IDataTypeService dataTypeService,
            ILogger<MigrateDataTypeConfigurationBase> logger)
            : base(context)
        {
            _scopeProvider = scopeProvider;
            _dataTypeService = dataTypeService;
            _logger = logger;
        }

        /// <summary>
        /// Updates a data type with the specified configuration.
        /// </summary>
        /// <param name="id">The data type ID.</param>
        /// <param name="editorUiAlias">The editor UI alias.</param>
        /// <param name="configurationJson">The configuration JSON.</param>
        /// <param name="expectedCurrentConfigurationJson">Optional JSON that must match the current configuration when the editor UI alias is unchanged.</param>
        /// <returns>The number of data types updated (0 or 1).</returns>
        protected async Task<int> UpdateDataTypeAsync(
            Guid id,
            string editorUiAlias,
            string configurationJson,
            string? expectedCurrentConfigurationJson = null)
        {
            try
            {
                using IScope scope = _scopeProvider.CreateScope(autoComplete: true);

                IDataType? dataType = await _dataTypeService.GetAsync(id);
                if (dataType is null)
                {
                    return 0;
                }

                if (dataType is not DataType dt)
                {
                    _logger.LogWarning("DataType with id {id} is not a concrete DataType, skipping.", id);
                    return 0;
                }

                var editorUiAliasChanged = !string.Equals(dt.EditorUiAlias, editorUiAlias, StringComparison.Ordinal);
                var wasChanged = false;

                if (editorUiAliasChanged)
                {
                    dt.EditorUiAlias = editorUiAlias;
                    wasChanged = true;
                }

                Dictionary<string, object> configObj = TryParseConfiguration(configurationJson, _logger);
                IDictionary<string, object> currentCfg = dt.ConfigurationData;
                Dictionary<string, object>? expectedCurrentConfig = expectedCurrentConfigurationJson is null
                    ? null
                    : TryParseConfiguration(expectedCurrentConfigurationJson, _logger);

                if ((editorUiAliasChanged
                     || (expectedCurrentConfig is not null
                         && EqualsConfig(currentCfg, expectedCurrentConfig, _logger)))
                    && !EqualsConfig(currentCfg, configObj, _logger))
                {
                    dt.ConfigurationData = configObj;
                    wasChanged = true;
                }

                if (!wasChanged)
                {
                    return 0;
                }

                Attempt<IDataType, DataTypeOperationStatus> update = await _dataTypeService.UpdateAsync(dt, Constants.Security.SuperUserKey);
                if (!update.Success)
                {
                    throw new InvalidOperationException(
                        $"Failed updating DataType id {id}: {update.Status}",
                        update.Exception);
                }

                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed updating DataType id {id}", id);
                throw;
            }
        }

        private static Dictionary<string, object> TryParseConfiguration(string json, ILogger logger)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new Dictionary<string, object>();
                }

                Dictionary<string, object>? dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                return dict ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse configuration JSON, using empty dictionary");
                return new Dictionary<string, object>();
            }
        }

        private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = false };

        private static bool EqualsConfig(object? a, object? b, ILogger logger)
        {
            try
            {
                JsonNode nodeA = ConvertToNode(a);
                JsonNode nodeB = ConvertToNode(b);
                return JsonNode.DeepEquals(nodeA, nodeB);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to compare configuration JSON, assuming difference to force update.");
                return false;
            }
        }

        private static JsonNode ConvertToNode(object? value)
        {
            if (value is JsonNode node)
            {
                return node;
            }

            string json = JsonSerializer.Serialize(value ?? new Dictionary<string, object?>(), _serializerOptions);
            var parsed = JsonNode.Parse(json);
            return parsed ?? JsonNode.Parse("{}")!;
        }
    }
}
