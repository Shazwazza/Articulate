#nullable enable
using Articulate.Attributes;
using Articulate.Models.Api;
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
    /// <summary>
    ///     Provides API endpoints for managing Articulate themes, including operations for copying a theme to a new name and
    ///     retrieving default themes.
    /// </summary>
    [ManagementApi(ArticulateConstants.ManagementApi.ThemeOptions)]
    [ApiVersion("1.0")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ManagementApiRoute("theme")]
    [MapToApi(ArticulateConstants.ManagementApi.Name)]
    public class ThemeOptionsApiController(
        IArticulateThemeRepository themeRepository,
        ILogger<ThemeOptionsApiController> logger) : ManagementApiControllerBase
    {
        /// <summary>
        ///     Copies a theme to a new theme name.
        /// </summary>
        /// <param name="model">The model containing the theme name to copy and the new theme name.</param>
        /// <returns>The new theme name.</returns>
        /// <response code="200">The theme was successfully copied to the new theme name.</response>
        /// <response code="400">The destination theme name matches a built-in theme.</response>
        /// <response code="404">The theme name specified in the model does not exist.</response>
        /// <response code="409">The new theme name specified in the model already exists.</response>
        /// <response code="500">An unexpected error occurred during the theme copy operation.</response>
        [HttpPost("copy")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Copy(ThemeCopyModel model)
        {
            try
            {
                await themeRepository.CopyThemeAsync(model.ThemeName, model.NewThemeName);
                return Ok(model.NewThemeName);
            }
            catch (DirectoryNotFoundException ex)
            {
                logger.LogWarning(
                    ex,
                    "Theme copy failed because the source theme '{ThemeName}' was not found.",
                    model.ThemeName);
                return NotFound(new ProblemDetails
                {
                    Title = "Theme Not Found", Detail = "The requested source theme could not be found."
                });
            }
            catch (ArgumentException ex) when (ex.ParamName == "newThemeName")
            {
                logger.LogWarning(
                    ex,
                    "Theme copy was rejected because the destination theme name '{NewThemeName}' is reserved.",
                    model.NewThemeName);
                return BadRequest(new ProblemDetails
                {
                    Title = "Reserved Theme Name", Detail = "Built-in theme names cannot be used for copied themes."
                });
            }
            catch (IOException ex)
            {
                logger.LogWarning(
                    ex,
                    "Theme copy failed because the destination theme '{NewThemeName}' already exists or could not be created.",
                    model.NewThemeName);
                return Conflict(new ProblemDetails
                {
                    Title = "Duplicate Theme Name",
                    Detail = "The destination theme already exists or could not be created."
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occurred during the theme copy operation.");
                return Problem(
                    "An unexpected error occurred during the theme copy operation.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        ///     Retrieves the list of default Articulate themes.
        /// </summary>
        /// <remarks>
        ///     This endpoint returns the names of all default themes available in the system.
        /// </remarks>
        /// <returns>
        ///     A list of default theme names as strings.
        /// </returns>
        /// <response code="200">Returns the list of default theme names.</response>
        /// <response code="500">An unexpected error occurred while retrieving default themes.</response>
        [HttpGet("default")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDefaultThemes()
        {
            try
            {
                IEnumerable<string> themes = await themeRepository.GetDefaultThemesAsync();
                return Ok(themes);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occurred while retrieving default themes.");
                return Problem(
                    "An error occurred while retrieving default themes.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
