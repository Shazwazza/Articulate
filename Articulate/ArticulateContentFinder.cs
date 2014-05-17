using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Articulate.Models;
using umbraco;
using umbraco.cms.helpers;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    /// <summary>
    /// Used to route custom articulate routes such as "/tags" "/tags/mytag" and "/search"
    /// </summary>
    public class ArticulateContentFinder : ContentFinderByNiceUrl
    {
        private static volatile UrlPossibilities _possibilities;
        private static readonly ReaderWriterLockSlim Locker = new ReaderWriterLockSlim();

        public override bool TryFindContent(PublishedContentRequest contentRequest)
        {
            var route = !contentRequest.HasDomain ? contentRequest.Uri.GetAbsolutePathDecoded() : contentRequest.Domain.RootNodeId + DomainHelper.PathRelativeToDomain(contentRequest.DomainUri, contentRequest.Uri.GetAbsolutePathDecoded());

            var parts = route.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {

                var urlPossibilities = GetPossibilities(contentRequest);


                return
                    //if it ends with '/search'
                    FindContent(route,
                        urlPossibilities.PossibleSearchNames,
                        possibilities => possibilities.Contains(parts[parts.Length - 1]),
                        contentRequest, "Search", "ArticulateList")
                    //if it ends with '/tags'
                    || FindContent(route,
                        urlPossibilities.PossibleTagsNames,
                        possibilities => possibilities.Contains(parts[parts.Length - 1]),
                        contentRequest, "Tags", "ArticulateArchive")
                    //if it ends with '/categories'
                    || FindContent(route,
                        urlPossibilities.PossibleCategoriesNames,
                        possibilities => possibilities.Contains(parts[parts.Length - 1]),
                        contentRequest, "Categories", "ArticulateArchive")
                    //if the 2nd last item is /tags
                    || FindContent(
                        route,
                        urlPossibilities.PossibleTagsNames,
                        possibilities => parts.Length > 1 && possibilities.Contains(parts[parts.Length - 2]),
                        contentRequest, "Tag", "ArticulateList",
                        pageName => parts[parts.Length - 1], 
                        urlSegment => urlSegment + "/" + parts[parts.Length - 1])
                    //if the 2nd last item is /categories
                    || FindContent(
                        route,
                        urlPossibilities.PossibleCategoriesNames,
                        possibilities => parts.Length > 1 && possibilities.Contains(parts[parts.Length - 2]),
                        contentRequest, "Category", "ArticulateList", 
                        pageName => parts[parts.Length - 1],
                        urlSegment => urlSegment + "/" + parts[parts.Length - 1]);
            }

            return false;
        }

        /// <summary>
        /// Looks up all 'Articulate' nodes and stores the 'possible' url names that might be used for search,tags,categories
        /// which limits the URLs that we will inspect.
        /// </summary>
        /// <param name="contentRequest"></param>
        private void EnsurePossibilities(PublishedContentRequest contentRequest)
        {
            if (_possibilities != null) return;
            using (new WriteLock(Locker))
            {
                if (_possibilities != null) return;

                var articulateNodes = contentRequest.RoutingContext.UmbracoContext.ContentCache.GetByXPath("//Articulate");
                var possibleSearchNames = new List<UrlPossibility>();
                var possibleTagsNames = new List<UrlPossibility>();
                var possibleCategoriesNames = new List<UrlPossibility>();
                foreach (var node in articulateNodes)
                {
                    possibleSearchNames.Add(
                        new UrlPossibility(node.GetPropertyValue<string>("searchUrlName"), node.GetPropertyValue<string>("searchPageName"), "searchUrlName"));
                    possibleTagsNames.Add(
                        new UrlPossibility(node.GetPropertyValue<string>("tagsUrlName"), node.GetPropertyValue<string>("tagsPageName"), "tagsUrlName"));
                    possibleCategoriesNames.Add(
                        new UrlPossibility(node.GetPropertyValue<string>("categoriesUrlName"), node.GetPropertyValue<string>("categoriesPageName"), "categoriesUrlName"));
                }

                _possibilities = new UrlPossibilities(possibleSearchNames, possibleTagsNames, possibleCategoriesNames);
            }
        }

        /// <summary>
        /// Returns the possibilities in a read lock
        /// </summary>
        /// <param name="contentRequest"></param>
        /// <returns></returns>
        private UrlPossibilities GetPossibilities(PublishedContentRequest contentRequest)
        {
            EnsurePossibilities(contentRequest);

            using (new ReadLock(Locker))
            {
                return _possibilities;
            }
        }

        /// <summary>
        /// Clears the possibility cache - used when content is updated with new names
        /// </summary>
        internal static void ClearPossibilities()
        {
            using (new WriteLock(Locker))
            {
                _possibilities = null;
            }
        }

        private bool FindContent(
            string currRoute,
            IEnumerable<UrlPossibility> urlSegmentPossibilities,
            Func<IEnumerable<string>, bool> urlCheck,
            PublishedContentRequest contentRequest,            
            string templateName,
            string docTypeAlias,
            Func<string, string> pageName = null,
            Func<string, string> urlPath = null)
        {
            if (urlCheck(urlSegmentPossibilities.Select(x => x.PageUrlSegment)))
            {
                foreach (var possibility in urlSegmentPossibilities)
                {
                    currRoute = currRoute.Substring(0, currRoute.LastIndexOf("/" + possibility.PageUrlSegment, StringComparison.InvariantCultureIgnoreCase) + 1);
                    //use base class to find by route
                    var parent = FindContent(contentRequest, currRoute);
                    if (parent != null && parent.DocumentTypeAlias.InvariantEquals("Articulate"))
                    {
                        //now verify that the property on the blog post page matches the current url possibility, this is required
                        // because if we have multiple Articulate root nodes and people have set differnt url segment names for 
                        // search, categories, tags than another articulate root node we don't want to match on all possibilities.
                        if (parent.GetPropertyValue<string>(possibility.UrlPropertyTypeName) == possibility.PageUrlSegment)
                        {
                            contentRequest.PublishedContent = new ArticulateVirtualPage(
                               parent,
                               pageName == null ? possibility.PageName : pageName(possibility.PageName),
                               docTypeAlias,
                               urlPath == null ? null : urlPath(possibility.PageUrlSegment));
                            //set the template name to specify which MVC Action to execute on the tags controller
                            contentRequest.SetTemplate(new Template("", templateName, templateName));
                            return true;   
                        }                        
                    }
                }


            }

            return false;
        }

        private class UrlPossibilities
        {
            public UrlPossibilities(List<UrlPossibility> possibleSearchNames, List<UrlPossibility> possibleTagsNames, List<UrlPossibility> possibleCategoriesNames)
            {
                PossibleSearchNames = possibleSearchNames;
                PossibleTagsNames = possibleTagsNames;
                PossibleCategoriesNames = possibleCategoriesNames;
            }

            public List<UrlPossibility> PossibleSearchNames { get; private set; }
            public List<UrlPossibility> PossibleTagsNames { get; private set; }
            public List<UrlPossibility> PossibleCategoriesNames { get; private set; }
        }

        private class UrlPossibility
        {
            public UrlPossibility(string pageUrlSegment, string pageName, string urlPropertyTypeName)
            {
                PageName = pageName;
                UrlPropertyTypeName = urlPropertyTypeName;
                PageUrlSegment = pageUrlSegment;
            }

            public string PageName { get; private set; }
            public string PageUrlSegment { get; private set; }
            public string UrlPropertyTypeName { get; private set; }
        }
    }
}