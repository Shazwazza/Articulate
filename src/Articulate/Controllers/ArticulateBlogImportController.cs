using Articulate.Models;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Articulate.ImportExport;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Extensions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace Articulate.Controllers
{
    public class ArticulateBlogImportController : UmbracoAuthorizedApiController
    {
        private readonly BlogMlImporter _blogMlImporter;
        private readonly UmbracoApiControllerTypeCollection _umbracoApiControllerTypeCollection;
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly BlogMlExporter _blogMlExporter;

        public ArticulateBlogImportController(
            IHostingEnvironment hostingEnvironment,
            BlogMlExporter blogMlExporter,
            BlogMlImporter blogMlImporter,
            UmbracoApiControllerTypeCollection umbracoApiControllerTypeCollection,
            IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
        {
            _blogMlImporter = blogMlImporter;
            _umbracoApiControllerTypeCollection = umbracoApiControllerTypeCollection;
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
            _hostingEnvironment = hostingEnvironment;
            _blogMlExporter = blogMlExporter;
        }

        public async Task<JObject> PostInitialize(HttpRequestMessage request)
        {
            throw new NotImplementedException("TODO: Implement file handling correctly");
            //if (!request.Content.IsMimeMultipartContent())
            //    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            //var dir = _hostingEnvironment.MapPathContentRoot("~/Umbraco/Data/TEMP/FileUploads");
            //Directory.CreateDirectory(dir);
            //var provider = new MultipartFormDataStreamProvider(dir);
            //var result = await request.Content.ReadAsMultipartAsync(provider);

            //if (result.FileData.Any())
            //{
            //    if (!Path.GetExtension(result.FileData[0].Headers.ContentDisposition.FileName.Trim('\"')).InvariantEquals(".xml"))
            //    {
            //        throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            //    }

            //    //there should only be one file so we'll just use the first one

            //    var count = _blogMlImporter.GetPostCount(result.FileData[0].LocalFileName);

            //    return JObject.FromObject(new
            //    {
            //        count = count,
            //        tempFile = result.FileData[0].LocalFileName
            //    });
            //}
            //else
            //{
            //    throw new InvalidOperationException("Not blogml file was found in the request");
            //}
        }

        public ImportModel PostExportBlogMl(ExportBlogMlModel model)
        {
            _blogMlExporter.Export("BlogMlExport.xml", model.ArticulateNodeId);

            return new ImportModel
            {
                DownloadUrl = Url.GetUmbracoApiService<ArticulateBlogImportController>(_umbracoApiControllerTypeCollection, "GetBlogMlExport")
            };
        }

        public HttpResponseMessage GetBlogMlExport()
        {
            //save to Temp folder (base path)
            var filePath = _hostingEnvironment.MapPathContentRoot("~/Umbraco/Data/TEMP/Articulate/BlogMlExport.xml");
            FileStream fileStream = System.IO.File.OpenRead(filePath);

            var result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(fileStream)
            };
            result.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "BlogMlExport.xml"
            };
            result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return result;
        }

        public async Task<ImportModel> PostImportBlogMl(ImportBlogMlModel model)
        {
            throw new NotImplementedException("TODO: Implement file handling correctly");

            //if (!ModelState.IsValid)
            //{
            //    throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            //}

            ////there should only be one file so we'll just use the first one

            //var hasErrors = await _blogMlImporter.Import(
            //    _backOfficeSecurityAccessor.BackOfficeSecurity.CurrentUser.Id,
            //    model.TempFile,
            //    model.ArticulateNodeId,
            //    model.Overwrite,
            //    model.RegexMatch,
            //    model.RegexReplace,
            //    model.Publish,
            //    model.ExportDisqusXml,
            //    model.ImportFirstImage);

            ////cleanup
            //System.IO.File.Delete(model.TempFile);

            //if (hasErrors)
            //{
            //    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Importing failed, see umbraco log for details"));
            //}

            //return new ImportModel
            //{
            //    DownloadUrl = Url.GetUmbracoApiService<ArticulateBlogImportController>(_umbracoApiControllerTypeCollection, "GetDisqusExport")
            //};
        }

        public HttpResponseMessage GetDisqusExport()
        {
            //save to Temp folder (base path)
            var filePath = _hostingEnvironment.MapPathContentRoot("~/Umbraco/Data/TEMP/Articulate/DisqusXmlExport.xml");
            FileStream fileStream = System.IO.File.OpenRead(filePath);

            var result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(fileStream)
            };
            result.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "DisqusXmlExport.xml"
            };
            result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return result;
        }
    }
}
