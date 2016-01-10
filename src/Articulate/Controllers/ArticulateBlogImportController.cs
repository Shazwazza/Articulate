using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.UI.WebControls;
using Articulate.Models;
using Newtonsoft.Json.Linq;
using umbraco.BusinessLogic;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Web;
using Umbraco.Web.WebApi;

namespace Articulate.Controllers
{
    [IsBackOffice]
    public class ArticulateBlogImportController : UmbracoApiController
    {
     
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

                //save to Temp folder (base path)
                var fs = new PhysicalFileSystem(IOHelper.MapPath("~/App_Data/Temp"));

                //there should only be one file so we'll just use the first one
                var importer = new BlogMlImporter(ApplicationContext, fs);
                var count = importer.GetPostCount(result.FileData[0].LocalFileName);

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

        public ImportModel PostExportBlogMl(ImportBlogMlModel model)
        {
            var mvcUrlHelper = new UrlHelper(new RequestContext());
            return new ImportModel
            {
                DownloadUrl = mvcUrlHelper.SurfaceAction<ArticulateBlogImportDataController>("DownloadDisqusExport")
            };            
        }

        public async Task<ImportModel> PostImportBlogMl(ImportBlogMlModel model)
        {
            if (!ModelState.IsValid)
            {
                throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            }

            //save to Temp folder (base path)
            var fs = new PhysicalFileSystem(IOHelper.MapPath("~/App_Data/Temp"));

            //there should only be one file so we'll just use the first one
            var importer = new BlogMlImporter(ApplicationContext, fs);
            await importer.Import(Security.CurrentUser.Id, 
                model.TempFile,
                model.ArticulateNodeId,
                model.Overwrite,
                model.RegexMatch, 
                model.RegexReplace, 
                model.Publish,
                model.ExportDisqusXml);
            
            //cleanup
            File.Delete(model.TempFile);

            if (importer.HasErrors)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Importing failed, see umbraco log for details"));
            }

            var mvcUrlHelper = new UrlHelper(new RequestContext());
            return new ImportModel
            {
                DownloadUrl = mvcUrlHelper.SurfaceAction<ArticulateBlogImportDataController>("DownloadDisqusExport")
            };
        }

    }
}