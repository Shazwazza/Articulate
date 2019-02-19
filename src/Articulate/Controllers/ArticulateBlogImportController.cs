using Articulate.Models;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.WebApi;

namespace Articulate.Controllers
{
    public class ArticulateBlogImportController : UmbracoAuthorizedApiController
    {
        private readonly BlogMlImporter _blogMlImporter;
        private readonly BlogMlExporter _blogMlExporter;

        public ArticulateBlogImportController(IGlobalSettings globalSettings, IUmbracoContextAccessor umbracoContextAccessor, ISqlContext sqlContext, ServiceContext services, AppCaches appCaches, IProfilingLogger logger, IRuntimeState runtimeState, UmbracoHelper umbracoHelper, BlogMlImporter blogMlImporter, BlogMlExporter blogMlExporter) : base(globalSettings, umbracoContextAccessor, sqlContext, services, appCaches, logger, runtimeState, umbracoHelper)
        {
            _blogMlImporter = blogMlImporter;
            _blogMlExporter = blogMlExporter;
        }
        
        public async Task<JObject> PostInitialize(HttpRequestMessage request)
        {
            if (!request.Content.IsMimeMultipartContent())
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            var dir = IOHelper.MapPath("~/App_Data/TEMP/FileUploads");
            Directory.CreateDirectory(dir);
            var provider = new MultipartFormDataStreamProvider(dir);
            var result = await request.Content.ReadAsMultipartAsync(provider);

            if (result.FileData.Any())
            {
                if (!Path.GetExtension(result.FileData[0].Headers.ContentDisposition.FileName.Trim('\"')).InvariantEquals(".xml"))
                {
                    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
                }

                //there should only be one file so we'll just use the first one
                
                var count = _blogMlImporter.GetPostCount(result.FileData[0].LocalFileName);

                return JObject.FromObject(new
                {
                    count = count,
                    tempFile = result.FileData[0].LocalFileName
                });
            }
            else
            {
                throw new InvalidOperationException("Not blogml file was found in the request");
            }
        }

        public ImportModel PostExportBlogMl(ExportBlogMlModel model)
        {
            _blogMlExporter.Export("BlogMlExport.xml", model.ArticulateNodeId);

            return new ImportModel
            {
                DownloadUrl = Url.GetUmbracoApiService<ArticulateBlogImportController>("GetBlogMlExport")
            };
        }

        public HttpResponseMessage GetBlogMlExport()
        {
            //save to Temp folder (base path)
            var fs = new PhysicalFileSystem("~/App_Data/Temp/Articulate");
            var fileStream = fs.OpenFile("BlogMlExport.xml");

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
            if (!ModelState.IsValid)
            {
                throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            }

            //there should only be one file so we'll just use the first one

            var hasErrors = await _blogMlImporter.Import(Security.CurrentUser.Id,
                model.TempFile,
                model.ArticulateNodeId,
                model.Overwrite,
                model.RegexMatch,
                model.RegexReplace,
                model.Publish,
                model.ExportDisqusXml,
                model.ImportFirstImage);

            //cleanup
            File.Delete(model.TempFile);

            if (hasErrors)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Importing failed, see umbraco log for details"));
            }

            return new ImportModel
            {
                DownloadUrl = Url.GetUmbracoApiService<ArticulateBlogImportController>("GetDisqusExport")
            };
        }

        public HttpResponseMessage GetDisqusExport()
        {
            //save to Temp folder (base path)
            var fs = new PhysicalFileSystem("~/App_Data/Temp/Articulate");
            var fileStream = fs.OpenFile("DisqusXmlExport.xml");

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