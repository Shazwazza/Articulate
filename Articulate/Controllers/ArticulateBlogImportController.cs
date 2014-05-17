using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
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

            if (result.FileData.Any())
            {
                if (!Path.GetExtension(result.FileData[0].LocalFileName).InvariantEquals(".xml"))
                {
                    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);    
                }

                //there should only be one file so we'll just use the first one
                var importer = new BlogMlImporter();
                importer.Import(result.FileData[0].LocalFileName);

                //cleanup
                File.Delete(result.FileData[0].LocalFileName);
            }

            return true;
        }

    }
}