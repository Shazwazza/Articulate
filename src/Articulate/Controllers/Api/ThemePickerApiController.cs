#nullable enable
using Articulate.Attributes;
using Articulate.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Web.Common.Authorization;

namespace Articulate.Controllers.Api
{
    // NOTE: ManagementApiControllerBase [ApiController] attribute will automatically validate the model
    // [ApiController] attribute also infers [FromBody] for model binding

    /// <summary>
    ///     Provides API endpoints for copying an Articulate default theme to a new theme name to allow customisation.
    /// </summary>
    [ManagementApi(ArticulateConstants.ManagementApi.ThemePicker)]
    [ApiVersion("1.0")]
    [Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
    [ManagementApiRoute("editors/theme-picker")]
    [MapToApi(ArticulateConstants.ManagementApi.Name)]
    public class ThemePickerApiController(
        IArticulateThemeRepository themeRepository,
        ILogger<ThemePickerApiController> logger) : ManagementApiControllerBase
    {
        /// <summary>
        ///     Gets the list of all available Articulate themes, both default and user-defined.
        /// </summary>
        /// <remarks>
        ///     This endpoint returns the names of all available themes, including both default and user-defined themes.
        /// </remarks>
        /// <returns>
        ///     A list of theme names as strings.
        /// </returns>
        /// <response code="200">Returns the list of all available theme names.</response>
        [HttpGet("themes")]
        [ProducesResponseType<IEnumerable<string>>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<string>>> GetAllThemes()
        {
            try
            {
                return Ok(await themeRepository.GetAllThemesAsync());
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occurred while retrieving themes");

                return Problem(
                    "An unexpected error occurred while retrieving themes.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
