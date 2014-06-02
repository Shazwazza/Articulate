using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.UI.WebControls;
using umbraco.BusinessLogic;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Web.WebApi;

namespace Articulate.Controllers
{
    public class ArticulateBlogImportController : UmbracoApiController
    {
        public async Task<bool> ImportBlogMl(HttpRequestMessage request)
        {
            if (!request.Content.IsMimeMultipartContent())
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            var dir = IOHelper.MapPath("~/App_Data/TEMP/FileUploads");
            Directory.CreateDirectory(dir);
            var provider = new MultipartFormDataStreamProvider(dir);
            var result = await request.Content.ReadAsMultipartAsync(provider);

            var importIdAttempt = result.FormData["articulateNode"].TryConvertTo<int>();
            if (!importIdAttempt)
            {
                throw new InvalidOperationException("An invalid articulate root node id was specified");
            }

            var overwriteAttempt = result.FormData["overwrite"].TryConvertTo<bool>();
            if (!overwriteAttempt)
            {
                throw new InvalidOperationException("An invalid overwrite value was specified");
            }

            var regexMatch = result.FormData["regexMatch"];
            var regexReplace = result.FormData["regexReplace"];
            var publishAttempt = result.FormData["publish"].TryConvertTo<bool>();
            if (!publishAttempt)
            {
                throw new InvalidOperationException("An invalid publish value was specified");
            }

            if (result.FileData.Any())
            {
                if (!Path.GetExtension(result.FileData[0].Headers.ContentDisposition.FileName.Trim('\"')).InvariantEquals(".xml"))
                {
                    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);    
                }

                //there should only be one file so we'll just use the first one
                var importer = new BlogMlImporter(ApplicationContext);
                importer.Import(Security.CurrentUser.Id, result.FileData[0].LocalFileName, importIdAttempt.Result, overwriteAttempt.Result, regexMatch, regexReplace, publishAttempt.Result);

                //cleanup
                File.Delete(result.FileData[0].LocalFileName);
            }

            return true;
        }

    }
}