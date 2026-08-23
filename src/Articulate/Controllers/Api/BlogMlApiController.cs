#nullable enable
using Articulate.Attributes;
using Articulate.ImportExport;
using Articulate.Models.Api;
using Articulate.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;

namespace Articulate.Controllers.Api
{
    /// <summary>
    /// Provides import and export of Articulate blog data using BlogML and Disqus formats.
    /// </summary>
    [ManagementApi(ArticulateConstants.ManagementApi.BlogMl)]
    [ApiVersion("1.0")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    // The Management API security filter documents 401/403. Repeating those response
    // attributes causes duplicate OpenAPI response keys and prevents schema generation.
    [ManagementApiRoute("blogml")]
    [MapToApi(ArticulateConstants.ManagementApi.Name)]
    public class BlogMlApiController(
        BlogMlExporter blogMlExporter,
        BlogMlImporter blogMlImporter,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        ArticulateTempFileSystem articulateTempFileSystem,
        IOptionsMonitor<Options.ArticulateOptions> articulateOptions,
        IOptionsMonitor<RuntimeSettings> runtimeSettings,
        ILogger<BlogMlApiController> logger)
        : ManagementApiControllerBase
    {
        /// <summary>
        /// Begins the BlogML import process by accepting an uploaded XML file.
        /// </summary>
        /// <param name="importFile">The file to import, must be an XML file in BlogML format.</param>
        /// <remarks>The name specified in the form's element or FormData must match the name of the parameter, e.g., <![CDATA[<input type="file" name="importFile">]]></remarks>
        /// <response code="200">Returns the temporary file name and post count.</response>
        /// <response code="415">The request is not a valid form file, file is missing, or the file is not XML.</response>
        /// <response code="500">Upload failed due to a server error.</response>
        [HttpPost("import-file")]
        [ProducesResponseType<ImportFileResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PostInitialize(IFormFile? importFile)
        {
            if (importFile is null)
            {
                return Problem(
                    title: "Invalid File",
                    detail: "The request must include a BlogML XML file.",
                    statusCode: StatusCodes.Status415UnsupportedMediaType);
            }

            if (!Path.GetExtension(importFile.FileName.Trim('\"')).InvariantEquals(".xml"))
            {
                return Problem(
                    title: "Invalid File",
                    detail: "Only BlogML .xml files are accepted.",
                    statusCode: StatusCodes.Status415UnsupportedMediaType);
            }

            if (importFile.Length <= 0)
            {
                return Problem(
                    title: "File Size Invalid",
                    detail: "The uploaded file is empty or invalid.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Note: File size limits are enforced by server configuration (Kestrel, IIS, FormOptions).
            // The server will return 413 Payload Too Large if the file exceeds configured limits.

            try
            {
                var fileName = Path.GetRandomFileName();
                await using Stream sourceStream = importFile.OpenReadStream();
                using var buffer = new MemoryStream();

                await sourceStream.CopyToAsync(buffer, HttpContext.RequestAborted);

                buffer.Position = 0;
                articulateTempFileSystem.AddFile(fileName, buffer);

                BlogMlImportFileSummary summary = blogMlImporter.GetImportFileSummary(fileName);
                Options.ArticulateOptions options = articulateOptions.CurrentValue;
                bool isProductionMode = runtimeSettings.CurrentValue.Mode == RuntimeMode.Production;
                bool allowUnsafeLocalExternalImageHosts =
                    !isProductionMode &&
                    options.AllowUnsafeLocalExternalImageHostsInDevelopment;
                ISet<string> allowedHosts = options.AllowedMediaHosts.ToHashSet(StringComparer.OrdinalIgnoreCase);
                string[] blockedExternalHosts = summary.ExternalHosts
                    .Where(host => ExternalImageHostPolicy.ValidateHost(
                        host,
                        allowedHosts,
                        allowUnsafeLocalExternalImageHosts,
                        isProductionMode) is not null)
                    .ToArray();

                return Ok(new ImportFileResponse
                {
                    TemporaryFileName = fileName,
                    PostCount = summary.PostCount,
                    ExternalImageCount = summary.ExternalImageCount,
                    ExternalHosts = summary.ExternalHosts,
                    BlockedExternalHosts = blockedExternalHosts
                });
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "An unexpected error occurred during file initialization for import.");
                return Problem(
                    title: "Internal Server Error",
                    detail: "An unexpected error occurred during file initialization for import.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Deletes a previously uploaded temporary BlogML import file.
        /// </summary>
        /// <param name="tempFile">The temporary file name returned by the import-file endpoint.</param>
        /// <response code="204">The temporary file was deleted or did not exist.</response>
        /// <response code="400">The temporary file name is invalid.</response>
        [HttpDelete("import-file")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        public IActionResult DeleteInitialize([FromQuery] string tempFile)
        {
            if (string.IsNullOrEmpty(tempFile) || tempFile.Contains('/') || tempFile.Contains('\\'))
            {
                return Problem(
                    title: "Invalid File Name",
                    detail: "The temporary file name is invalid.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (articulateTempFileSystem.FileExists(tempFile))
            {
                articulateTempFileSystem.DeleteFile(tempFile);
            }

            return NoContent();
        }

        /// <summary>
        /// Exports blog data as a BlogML XML file.
        /// </summary>
        /// <param name="model">The export options including the Articulate node ID and image export settings.</param>
        /// <response code="200">Returns the BlogML XML file as a downloadable stream.</response>
        /// <response code="500">Export failed due to a server error.</response>
        /// <response code="503">The service is unavailable or the blog node is invalid.</response>
        [HttpPost("export")]
        [Produces("application/octet-stream", Type = typeof(FileContentResult))]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> PostExportBlogMl(ExportModel model)
        {
            var exportFileName = $"BlogMlExport-{Guid.NewGuid()}.xml";
            var exportSucceeded = false;
            try
            {
                await blogMlExporter.ExportAsync(model.ArticulateBlogNode, exportFileName, model.ExportImagesAsBase64);
                var downloadFileName = $"articulate-export-{DateTime.UtcNow:yyyyMMddHHmmss}.xml";

                Stream? fileStream = null;
                try
                {
                    Stream exportStream = articulateTempFileSystem.OpenFile(exportFileName);
                    fileStream = exportStream;

                    Response.OnCompleted(() =>
                    {
                        exportStream.Dispose();
                        articulateTempFileSystem.DeleteFile(exportFileName);
                        return Task.CompletedTask;
                    });

                    Response.Headers.Append("Content-Disposition", $"attachment; filename*=UTF-8''{downloadFileName}");
                    exportSucceeded = true;
                    return File(exportStream, "application/octet-stream");
                }
                catch
                {
                    if (fileStream is not null)
                    {
                        await fileStream.DisposeAsync();
                    }

                    throw;
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(
                    ex,
                    "Export failed due to an invalid operation, likely a missing or invalid blog node.");
                return Problem(
                    title: "Service Unavailable",
                    detail: "The requested blog export could not be completed because the Articulate blog node was unavailable or invalid.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "An unexpected error occurred during BlogML export.");
                return Problem(
                    title: "Internal Server Error",
                    detail: "An unexpected error occurred during BlogML export.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            finally
            {
                // Clean up temp file on failure (success path uses Response.OnCompleted)
                if (!exportSucceeded && articulateTempFileSystem.FileExists(exportFileName))
                {
                    try
                    {
                        articulateTempFileSystem.DeleteFile(exportFileName);
                    }
                    catch
                    {
                        // Best effort cleanup
                        logger.LogWarning("An error occurred while deleting the temporary export file '{ExportFileName}'.", exportFileName);
                    }
                }
            }
        }

        /// <summary>
        /// Imports blog data from a previously uploaded BlogML XML file.
        /// </summary>
        /// <param name="model">The import options including the temporary file name, Articulate node ID, and import settings.</param>
        /// <response code="200">Returns import statistics.</response>
        /// <response code="400">The requested Articulate node ID could not be found or is not a valid Articulate node.</response>
        /// <response code="404">The requested temporary file could not be found.</response>
        /// <response code="500">Import failed due to a server error.</response>
        [HttpPost("import")]
        [ProducesResponseType<ImportResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PostImportBlogMl(ImportModel model)
        {
            IUser? currentUser = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
            if (currentUser is null)
            {
                return Problem(
                    title: "Unauthorized",
                    detail: "Could not determine the current user.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (string.IsNullOrEmpty(model.TempFile) || model.TempFile.Contains('/') || model.TempFile.Contains('\\'))
            {
                return Problem(
                    title: "Invalid File Name",
                    detail: "The temporary file name is invalid.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                ImportResponseDto dto = await blogMlImporter.ImportAsync(
                    currentUser.Id,
                    model.TempFile,
                    model.ArticulateBlogNode,
                    model.Overwrite,
                    model.RegexMatch,
                    model.RegexReplace,
                    model.Publish,
                    model.ExportDisqusXml,
                    model.ImportFirstImage);

                var result = new ImportResponse(dto);

                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                logger.LogError(ex, "Importing failed because a file was not found.");
                return Problem(
                    title: "File Not Found",
                    detail: "The requested import file could not be found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Importing failed due to an invalid operation.");
                return Problem(
                    title: "Bad Request",
                    detail: "The BlogML import request was invalid or the selected Articulate node could not be used.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Importing failed due to an unexpected error.");
                return Problem(
                    title: "Internal Server Error",
                    detail: "An unexpected error occurred during import.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            finally
            {
                if (!string.IsNullOrEmpty(model.TempFile) && articulateTempFileSystem.FileExists(model.TempFile))
                {
                    articulateTempFileSystem.DeleteFile(model.TempFile);
                }
            }
        }

        /// <summary>
        /// Downloads the exported Disqus comment XML file.
        /// </summary>
        /// <response code="200">Returns the Disqus comment XML file as a downloadable stream.</response>
        /// <response code="404">The Disqus XML export file could not be found.</response>
        [HttpGet("export/disqus")]
        [Produces("application/octet-stream", Type = typeof(FileContentResult))]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public IActionResult GetDisqusExport()
        {
            IUser? currentUser = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
            if (currentUser is null)
            {
                return Problem(
                    title: "Unauthorized",
                    detail: "Could not determine the current user.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var disqusExportFile = $"DisqusXmlExport-{currentUser.Id}.xml";
            if (!articulateTempFileSystem.FileExists(disqusExportFile))
            {
                return Problem(
                    title: "File Not Found",
                    detail: "Disqus comments export file not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var downloadFileName = $"articulate-disqus-comments-{DateTime.UtcNow:yyyyMMddHHmmss}.xml";
            Stream? fileStream = null;

            try
            {
                Stream exportStream = articulateTempFileSystem.OpenFile(disqusExportFile);
                fileStream = exportStream;

                Response.OnCompleted(() =>
                {
                    exportStream.Dispose();
                    articulateTempFileSystem.DeleteFile(disqusExportFile);
                    return Task.CompletedTask;
                });

                Response.Headers.Append("Content-Disposition", $"attachment; filename*=UTF-8''{downloadFileName}");
                return File(exportStream, "application/octet-stream");
            }
            catch
            {
                fileStream?.Dispose();
                throw;
            }
        }
    }
}
