using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.UI.WebControls;
using Articulate.Models;
using Newtonsoft.Json.Linq;
using umbraco.BusinessLogic;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Web.WebApi;

namespace Articulate.Controllers
{
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

                //there should only be one file so we'll just use the first one
                var importer = new BlogMlImporter(ApplicationContext);
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

        public async Task<HttpResponseMessage> PostImportBlogMl(ImportBlogMlModel model)
        {
            if (!ModelState.IsValid)
            {
                throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            }
            
            //there should only be one file so we'll just use the first one
            var importer = new BlogMlImporter(ApplicationContext);
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
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Importing failed, see umbraco log for details");
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

    }
}