#nullable enable
using System.Reflection;
using Articulate.Attributes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Articulate
{
    /// <summary>
    ///     Extensions for <see cref="IApiDescriptionGroupCollectionProvider" />.
    /// </summary>
    public static class ApiExplorerExtensions
    {
        /// <summary>
        ///     Generates a mapping of Controller.Action to relative URL for the specified management API groups.
        /// </summary>
        /// <param name="provider">The API description provider.</param>
        /// <param name="apiGroupNames">The groups to include in the mapping.</param>
        /// <returns>A dictionary mapping action names to relative URLs.</returns>
        public static IReadOnlyDictionary<string, string>? ManagementApiUrlMap(
            this IApiDescriptionGroupCollectionProvider? provider,
            string[] apiGroupNames)
        {
            if (provider?.ApiDescriptionGroups.Items is null)
            {
                return null;
            }

            var groupNameSet = new HashSet<string>(apiGroupNames, StringComparer.OrdinalIgnoreCase);

            return provider.ApiDescriptionGroups.Items.Where(group =>
                    group.GroupName is not null && groupNameSet.Contains(group.GroupName))
                .SelectMany(group => group.Items)
                .Where(desc =>
                {
                    if (desc.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
                    {
                        return false;
                    }

                    return controllerActionDescriptor.ControllerTypeInfo
                        .GetCustomAttribute<ManagementApiAttribute>() is not null;
                })
                .ToDictionary(
                    desc =>
                    {
                        var cad = (ControllerActionDescriptor)desc.ActionDescriptor;
                        return $"{cad.ControllerTypeInfo.Name}.{cad.ActionName}";
                    },
                    desc => $"/{desc.RelativePath?.TrimStart('/')}");
        }
    }
}
