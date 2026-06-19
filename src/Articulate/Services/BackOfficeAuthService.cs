#nullable enable
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Articulate.Services
{
    /// <summary>
    ///     Service for checking back-office authentication and permissions.
    /// </summary>
    public sealed class BackOfficeAuthService(
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUserService userService,
        ILogger<BackOfficeAuthService> logger)
    {
        /// <summary>
        ///     Checks if a back-office user is logged in.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="authenticationType">The authentication type to check.</param>
        /// <returns>True if the user is logged in; otherwise, false.</returns>
        public async Task<bool> IsBackOfficeLoggedInAsync(HttpContext context, string authenticationType)
        {
            try
            {
                AuthenticateResult authenticateResult = await context.AuthenticateAsync(authenticationType);
                return authenticateResult is
                           { Succeeded: true, Principal.Identity.IsAuthenticated: true, Ticket: not null }
                       && string.Equals(
                           authenticateResult.Ticket.AuthenticationScheme,
                           authenticationType,
                           StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Back-office authentication check failed for {AuthType}", authenticationType);
                return false;
            }
        }

        /// <summary>
        ///     Gets the current back-office user.
        /// </summary>
        /// <returns>The current user, or null if not found.</returns>
        public IUser? GetCurrentUser() => backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;

        /// <summary>
        ///     Checks if a back-office user has specified permissions for a content item.
        /// </summary>
        /// <param name="user">The user to check.</param>
        /// <param name="contentItem">The content item to check permissions against.</param>
        /// <param name="permissionsToCheck">The collection of permissions to verify.</param>
        /// <returns>True if the user has all the specified permissions; otherwise, false.</returns>
        public bool HasPermissions(IUser user, IContent contentItem, IEnumerable<string>? permissionsToCheck)
        {
            List<string> permissionsToCheckList = permissionsToCheck?.ToList() ?? [];
            if (permissionsToCheckList.Count == 0)
            {
                return false;
            }

            IEnumerable<string> permissions = user.GetPermissions(contentItem.Path, userService);
            return permissionsToCheckList.All(permissions.Contains);
        }
    }
}
