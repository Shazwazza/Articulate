using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;

#nullable enable
namespace Articulate
{
    internal static class ContentExtensions
    {
        internal static async Task<IContent> CreateWithInvariantOrDefaultCultureNameAsync(
            this IContentService contentService,
            string name,
            IContent parent,
            IContentTypeComposition contentType,
            ILanguageService languageService,
            ILogger? logger = null,
            int userId = -1)
        {
            IContent content = contentService.Create(name, parent, contentType.Alias, userId);
            await content.SetInvariantOrDefaultCultureNameAsync(name, contentType, languageService, logger);
            return content;
        }

        internal static async Task<IContent> CreateWithInvariantOrDefaultCultureNameAsync(
            this IContentService contentService,
            string name,
            int parent,
            IContentTypeComposition contentType,
            ILanguageService languageService,
            ILogger? logger = null,
            int userId = -1)
        {
            IContent content = contentService.Create(name, parent, contentType.Alias, userId);
            await content.SetInvariantOrDefaultCultureNameAsync(name, contentType, languageService, logger);
            return content;
        }

        internal static async Task SetInvariantOrDefaultCultureNameAsync(
            this IContentBase content,
            string name,
            IContentTypeComposition contentType,
            ILanguageService languageService,
            ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(contentType);

            var variesByCulture = contentType.VariesByCulture();

            if (variesByCulture)
            {
                string? culture = null;
                try
                {
                    culture = await languageService.GetDefaultIsoCodeAsync();
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(
                        ex,
                        "Failed to get default culture for {ContentName}, falling back to invariant.",
                        name);
                }

                content.SetCultureName(name, culture);
                return;
            }

            content.Name = name;
        }

        /// <summary>
        /// Sets property values for all cultures or invariant, depending on content type variance.
        /// </summary>
        /// <remarks>
        /// Only sets values for cultures already defined on the content item.
        /// </remarks>
        internal static async Task SetInvariantOrDefaultCultureValueAsync(
            this IContentBase content,
            string propertyTypeAlias,
            object? value,
            IContentTypeComposition contentType,
            ILanguageService languageService,
            ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(contentType);

            var variesByCulture = VariesByCulture(propertyTypeAlias, contentType);

            string? culture = null;
            if (variesByCulture)
            {
                try
                {
                    culture = await languageService.GetDefaultIsoCodeAsync();
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(
                        ex,
                        "Failed to get default culture for property {PropertyAlias}, falling back to invariant.",
                        propertyTypeAlias);
                }
            }

            content.SetValue(propertyTypeAlias, value, culture);
        }

        internal static async Task AssignInvariantOrDefaultCultureTagsAsync(
            this IContentBase content,
            string propertyTypeAlias,
            IEnumerable<string> tags,
            IContentTypeComposition contentType,
            ILanguageService languageService,
            IDataTypeService dataTypeService,
            PropertyEditorCollection dataEditors,
            IJsonSerializer jsonSerializer,
#if UMBRACO_18_OR_GREATER
            IIdKeyMap idKeyMap,
#endif
            ILogger? logger = null,
            bool merge = false)
        {
            ArgumentNullException.ThrowIfNull(contentType);

            var variesByCulture = VariesByCulture(propertyTypeAlias, contentType);

            string? culture = null;
            if (variesByCulture)
            {
                try
                {
                    culture = await languageService.GetDefaultIsoCodeAsync();
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(
                        ex,
                        "Failed to get default culture for tags property {PropertyAlias}, falling back to invariant.",
                        propertyTypeAlias);
                }
            }

#if UMBRACO_18_OR_GREATER
            content.AssignTags(
                dataEditors,
                dataTypeService,
                idKeyMap,
                jsonSerializer,
                propertyTypeAlias,
                tags,
                merge,
                culture);
#else
            content.AssignTags(
                dataEditors,
                dataTypeService,
                jsonSerializer,
                propertyTypeAlias,
                tags,
                merge,
                culture);
#endif
        }

        internal static void SetAllPropertyCultureValues(
            this IContentBase content,
            string propertyAlias,
            IContentTypeComposition contentType,
            Func<IContentBase, IContentTypeComposition, ContentCultureInfos?, object?> propertyValueGetter)
        {
            ArgumentNullException.ThrowIfNull(contentType);

            if (content.ContentType.VariesByCulture() && content.CultureInfos is not null)
            {
                SetCultureVariantPropertyValues(content, propertyAlias, contentType, propertyValueGetter);
            }
            else
            {
                SetInvariantPropertyValue(content, propertyAlias, contentType, propertyValueGetter);
            }
        }

        private static void SetCultureVariantPropertyValues(
            IContentBase content,
            string propertyAlias,
            IContentTypeComposition contentType,
            Func<IContentBase, IContentTypeComposition, ContentCultureInfos?, object?> propertyValueGetter)
        {
            IPropertyType propertyType =
                contentType.CompositionPropertyTypes.FirstOrDefault(x => x.Alias == propertyAlias)
                ?? throw new InvalidOperationException($"No property type found by alias {propertyAlias}");
            foreach (ContentCultureInfos c in content.CultureInfos!)
            {
                var valueToSet = propertyValueGetter(content, contentType, c);
                if (IsNullOrEmptyValue(valueToSet))
                {
                    continue;
                }

                content.SetValue(propertyAlias, valueToSet, propertyType.VariesByCulture() ? c.Culture : null);
            }
        }

        private static void SetInvariantPropertyValue(
            IContentBase content,
            string propertyAlias,
            IContentTypeComposition contentType,
            Func<IContentBase, IContentTypeComposition, ContentCultureInfos?, object?> propertyValueGetter)
        {
            var propertyValue = propertyValueGetter(content, contentType, null);
            if (IsNullOrEmptyValue(propertyValue))
            {
                return;
            }

            content.SetValue(propertyAlias, propertyValue);
        }

        private static bool IsNullOrEmptyValue(object? value)
        {
            return value is null || (value is string propValAsString && string.IsNullOrWhiteSpace(propValAsString));
        }

        private static bool VariesByCulture(string propertyTypeAlias, IContentTypeComposition contentType)
        {
            // will throw if the property type is not found
            var variesByCulture = contentType.VariesByCulture() && contentType.CompositionPropertyTypes
                .First(x => x.Alias.InvariantEquals(propertyTypeAlias)).VariesByCulture();

            return variesByCulture;
        }
    }
}
