#nullable enable
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Articulate.Attributes;
using Articulate.MetaWeblog;
using Articulate.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using WilderMinds.MetaWeblog;

namespace Articulate.Controllers
{
    /// <summary>
    /// Custom controller to handle the weblog endpoints so that we can wire
    /// up the articulate start node for the IMetaWeblogProvider data source.
    /// </summary>
    /// <remarks>
    /// The nuget package we use https://github.com/shawnwildermuth/MetaWeblog has
    /// middleware but that just supports one endpoint, we are basically wrapping that
    /// with our own multi-tenanted version.
    /// </remarks>
    [ArticulateDynamicRoute]
    public class MetaWeblogController(
        ILogger<MetaWeblogController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IServiceProvider serviceProvider,
        IOptionsMonitor<ArticulateOptions> articulateOptions)
        : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
    {
        // This caps the XML-RPC request envelope, not the decoded image. MetaWeblog media uploads
        // send base64 image bytes (~4/3 raw size) inside XML with method parameters and metadata,
        // while DecodeAndValidateBase64ImageAsync still enforces MaxImportImageBytes on the image.
        private const int RequestBodyLimitMultiplier = 2;

        /// <summary>
        /// Handles MetaWeblog XML-RPC requests for the specified blog node.
        /// </summary>
        /// <param name="id">The node ID of the Articulate blog root.</param>
        /// <returns>
        /// A 200 XML-RPC response with the MetaWeblog API result, a 400 if the node ID, size configuration,
        /// or XML-RPC envelope is invalid, or a 413 if the request body exceeds the configured size limit.
        /// </returns>
        [HttpPost]
        public async Task<ActionResult> IndexAsync(int id)
        {
            if (!articulateOptions.CurrentValue.EnableMetaWeblog)
            {
                return NotFound();
            }

            if (id <= 0)
            {
                return Problem("Invalid root node id");
            }

            long maxImportImageBytes = articulateOptions.CurrentValue.MaxImportImageBytes;
            if (maxImportImageBytes <= 0)
            {
                return Problem("MaxImportImageBytes must be greater than zero");
            }
            if (maxImportImageBytes > long.MaxValue / RequestBodyLimitMultiplier)
            {
                return Problem("MaxImportImageBytes is too large");
            }

            long maxRequestBodyBytes = maxImportImageBytes * RequestBodyLimitMultiplier;

            if (Request.ContentLength is { } contentLength && contentLength > maxRequestBodyBytes)
            {
                return StatusCode(413, $"Request body exceeds the configured limit of {maxRequestBodyBytes} bytes");
            }

            // create the provider using the start node
            ArticulateMetaWeblogProvider provider = ActivatorUtilities.CreateInstance<ArticulateMetaWeblogProvider>(
                serviceProvider,
                id);

            // create the service using the provider
            MetaWeblogService service = ActivatorUtilities.CreateInstance<MetaWeblogService>(serviceProvider, provider);

            string rawContent;
            using var reader = new StreamReader(
                new SizeLimitedStream(Request.Body, maxRequestBodyBytes),
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            try
            {
                rawContent = await reader.ReadToEndAsync(HttpContext.RequestAborted);
            }
            catch (InvalidOperationException)
            {
                return StatusCode(413, $"Request body exceeds the configured limit of {maxRequestBodyBytes} bytes");
            }

            if (!TryNormalizeMetaWeblogRequest(rawContent, maxRequestBodyBytes, out string normalized))
            {
                return BadRequest("Invalid XML-RPC request envelope.");
            }

            try
            {
                var result = await service.InvokeAsync(normalized);
                return Content(result, "text/xml", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected exception occurred in metaWeblog service.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        internal static bool TryNormalizeMetaWeblogRequest(
            string rawContent,
            long maxCharactersInDocument,
            out string normalized)
        {
            normalized = string.Empty;
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = maxCharactersInDocument,
                };

                using var reader = XmlReader.Create(new StringReader(rawContent), settings);
                var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);

                if (document.Root?.Name.LocalName != "methodCall"
                    || document.Descendants("methodName").FirstOrDefault() is null)
                {
                    return false;
                }

                foreach (XElement nameElement in document.Descendants("name"))
                {
                    if (nameElement.Value == "date_created_gmt")
                    {
                        nameElement.Value = "dateCreated";
                    }
                }

                XElement? methodName = document.Descendants("methodName").FirstOrDefault();
                XElement? parameters = document.Root.Element("params");
                if (methodName?.Value == "wp.getUsersBlogs"
                    && parameters?.Elements("param").Count() == 2)
                {
                    // Open Live Writer retries with two arguments; add the unused first parameter.
                    methodName.Value = "blogger.getUsersBlogs";
                    parameters.AddFirst(
                        new XElement("param", new XElement("value", new XElement("string", string.Empty))));
                }

                normalized = document.ToString(SaveOptions.DisableFormatting);
                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        internal static bool IsValidXmlRpcEnvelope(string content) =>
            TryNormalizeMetaWeblogRequest(content, long.MaxValue, out _);
    }
}
