using System;
using Articulate.Models;
using umbraco;
using umbraco.cms.helpers;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web.Routing;

namespace Articulate
{
    /// <summary>
    /// Used to route custom articulate routes such as "/tags" "/tags/mytag" and "/search"
    /// </summary>
    public class ArticulateContentFinder : ContentFinderByNiceUrl
    {
        public override bool TryFindContent(PublishedContentRequest contentRequest)
        {
            var route = !contentRequest.HasDomain ? contentRequest.Uri.GetAbsolutePathDecoded() : contentRequest.Domain.RootNodeId + DomainHelper.PathRelativeToDomain(contentRequest.DomainUri, contentRequest.Uri.GetAbsolutePathDecoded());
            
            var parts = route.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {

                //TODO: Need to figure out how to get these URL names from the master Articulate node
                

                return
                    //if it ends with '/search'
                    FindContent(() => parts[parts.Length - 1].InvariantEquals("search"),
                        route, contentRequest, "search", () => "Search", "Search", "ArticulateList")
                        //if it ends with '/tags'
                    || FindContent(() => parts[parts.Length - 1].InvariantEquals("tags"),
                        route, contentRequest, "tags", () => "Tags", "Tags", "ArticulateArchive")
                        //if it ends with '/categories'
                    || FindContent(() => parts[parts.Length - 1].InvariantEquals("categories"),
                        route, contentRequest, "categories", () => "Categories", "Categories", "ArticulateArchive")
                        //if the 2nd last item is /tags
                    || FindContent(() => parts.Length > 1 && parts[parts.Length - 2].InvariantEquals("tags"),
                        route, contentRequest, "tags", () => parts[parts.Length - 1], "Tag", "ArticulateList", () => "tags/" + parts[parts.Length - 1])
                    //if the 2nd last item is /categories
                    || FindContent(() => parts.Length > 1 && parts[parts.Length - 2].InvariantEquals("categories"),
                        route, contentRequest, "categories", () => parts[parts.Length - 1], "Category", "ArticulateList", () => "categories/" + parts[parts.Length - 1]);                
            }
            
            return false;
        }

        private bool FindContent(
            Func<bool> urlCheck, 
            string currRoute, 
            PublishedContentRequest contentRequest, 
            string urlName, 
            Func<string> pageName, 
            string templateName, 
            string docTypeAlias,
            Func<string> urlPath = null)
        {
            if (urlCheck())
            {
                currRoute = currRoute.Substring(0, currRoute.LastIndexOf("/" + urlName, StringComparison.InvariantCultureIgnoreCase) + 1);
                var parent = FindContent(contentRequest, currRoute);
                if (parent != null && parent.DocumentTypeAlias.InvariantEquals("Articulate"))
                {
                    contentRequest.PublishedContent = new ArticulateVirtualPage(parent, pageName(), docTypeAlias, 
                        urlPath == null ? null : urlPath());
                    //set the template name to specify which MVC Action to execute on the tags controller
                    contentRequest.SetTemplate(new Template("", templateName, templateName));
                    return true;
                }
            }

            return false;
        }
    }
}