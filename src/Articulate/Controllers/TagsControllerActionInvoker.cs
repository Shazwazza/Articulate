// TODO: Not sure we'll need this since all routing is going to have to be dynamic anyways.

//using System.Collections.Concurrent;
//using System.Linq;

//namespace Articulate.Controllers
//{
//    /// <summary>
//    /// Selects the correct action to execute depending on what the categories or tags urls defined in the published content is
//    /// </summary>
//    internal class TagsControllerActionInvoker : AsyncControllerActionInvoker
//    {
//        private readonly ConcurrentDictionary<string, ReflectedActionDescriptor> _descriptorCache = new ConcurrentDictionary<string, ReflectedActionDescriptor>(); 

//        protected override ActionDescriptor FindAction(ControllerContext controllerContext, ControllerDescriptor controllerDescriptor, string actionName)
//        {
//            var ad = base.FindAction(controllerContext, controllerDescriptor, actionName);

//            //now we need to check if it exists, if not we need to return the Index by default
//            if (ad == null)
//            {
//                if (controllerContext.RouteData.DataTokens.ContainsKey("umbraco"))
//                {
//                    var virtualNode = controllerContext.RouteData.DataTokens["umbraco"] as ContentModel;
//                    if (virtualNode != null)
//                    {
//                        var action = controllerContext.RouteData.GetRequiredString("action");
                        
//                        var categoryUrl = virtualNode.Content.Value<string>("categoriesUrlName");
//                        if (action.InvariantEquals(categoryUrl))
//                        {
//                            return GetActionDescriptor(controllerContext, controllerDescriptor, "Categories");                            
//                        }

//                        var tagsUrl = virtualNode.Content.Value<string>("tagsUrlName");
//                        if (action.InvariantEquals(tagsUrl))
//                        {
//                            return GetActionDescriptor(controllerContext, controllerDescriptor, "Tags"); 
//                        }
//                    }
//                }
//            }
//            return ad;
//        }



//        /// <summary>
//        /// Gets the action descriptor and caches it
//        /// </summary>
//        /// <param name="controllerContext"></param>
//        /// <param name="controllerDescriptor"></param>
//        /// <param name="actionName"></param>
//        /// <returns></returns>
//        protected ReflectedActionDescriptor GetActionDescriptor(ControllerContext controllerContext, ControllerDescriptor controllerDescriptor, string actionName)
//        {
//            var found = _descriptorCache.GetOrAdd(
//                controllerDescriptor.UniqueId,
//                s => new ReflectedActionDescriptor(
//                    controllerContext.Controller.GetType().GetMethods()
//                        .First(x => x.Name == actionName &&
//                                    x.GetCustomAttributes(typeof(NonActionAttribute), false).Any() == false),
//                    actionName,
//                    controllerDescriptor));

//            //check if the action name matches, it won't if the user has changed the action name in umbraco, if this occurs we need to refresh the cache
//            if (!found.ActionName.InvariantEquals(actionName))
//            {
//                var newDescriptor = new ReflectedActionDescriptor(
//                    controllerContext.Controller.GetType().GetMethods()
//                        .First(x => x.Name == actionName &&
//                                    x.GetCustomAttributes(typeof (NonActionAttribute), false).Any() == false),
//                    actionName,
//                    controllerDescriptor);
                
//                _descriptorCache.TryUpdate(controllerDescriptor.UniqueId, newDescriptor, found);

//                found = newDescriptor;
//            }

//            return found;
//        }
//    }
//}
