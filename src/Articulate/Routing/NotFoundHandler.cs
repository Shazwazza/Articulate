using System.Web;

namespace Articulate.Routing
{
    public class NotFoundHandler : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            context.Response.StatusCode = 404;
        }

        public bool IsReusable => true;
    }
}