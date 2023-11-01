using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Articulate.Routing
{
    internal class ArticulateFrontEndFilterConvention : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                controller.Filters.Add(new RouteCacheRefresherFilter());
            }
        }
    }
}
