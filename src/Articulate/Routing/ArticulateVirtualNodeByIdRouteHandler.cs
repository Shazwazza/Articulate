using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Mvc;

namespace Articulate.Routing
{
    public class ArticulateVirtualNodeByIdRouteHandler : UmbracoVirtualNodeRouteHandler
    {
        private readonly Dictionary<Uri, int> _domainsAndIds = null;
        private readonly ILogger _logger;
        private readonly Lazy<Dictionary<Uri, int>> _lazyDomainsAndIds;

        private IDictionary<Uri, int> DomainsAndIds => _domainsAndIds ?? _lazyDomainsAndIds.Value;

        private static readonly Uri EmptyUri = new Uri("empty://");

        /// <summary>
        /// Constructor used to create a new handler for multi-tenency with domains and ids
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="contentUrls"></param>
        /// <param name="itemsForRoute"></param>
        public ArticulateVirtualNodeByIdRouteHandler(ILogger logger, ContentUrls contentUrls, IEnumerable<IPublishedContent> itemsForRoute)
        {
            _logger = logger;
            _lazyDomainsAndIds = new Lazy<Dictionary<Uri, int>>(() =>
            {
                //make this lazy so we can reduce the startup overhead

                var domainsAndIds = new Dictionary<Uri, int>();
                foreach (var publishedContent in itemsForRoute)
                {
                    var allUrls = contentUrls.GetContentUrls(publishedContent);

                    foreach (var url in allUrls)
                    {
                        //if there is a double slash, it will have a domain
                        if (url.Contains("//"))
                        {
                            var uri = new Uri(url.ToLowerInvariant(), UriKind.Absolute);
                            domainsAndIds[uri] = publishedContent.Id;
                        }
                        else
                        {
                            domainsAndIds[EmptyUri] = publishedContent.Id;
                        }
                    }

                    _logger.Debug<ArticulateVirtualNodeByIdRouteHandler>("Hosts/IDs map for node {NodeId}. Values: {ArticulateHostValues}", publishedContent.Id, DebugHostIdsCollection(domainsAndIds));
                    
                }

                return domainsAndIds;
            });

            
        }

        /// <summary>
        /// Constructor used to create a new handler for only one id and no domain
        /// </summary>
        /// <param name="realNodeId"></param>
        public ArticulateVirtualNodeByIdRouteHandler(int realNodeId)
        {
            _domainsAndIds = new Dictionary<Uri, int>() {[EmptyUri] = realNodeId};
        }

        protected sealed override IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext)
        {
            //determine if it's for a particular domain
            int realNodeId = 0;
            if (DomainsAndIds.Count == 1)
            {
                realNodeId = DomainsAndIds.Values.First();
            }
            else
            {
                if (requestContext.HttpContext.Request.Url == null)
                {
                    if (DomainsAndIds.Count > 0)
                    {
                        //cannot be determined
                        realNodeId = DomainsAndIds.Values.First();
                    }
                    else
                    {
                        _logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found to map hosts and IDs");
                        return null;
                    }
                }
                else
                {
                    //TODO: Continue this: First match hosts
                    // Then we can determine if there are more than one match, in which case we'll have to try to match some path prefixes
                    var hostMatches = DomainsAndIds.Where(x => Uri.Compare(x.Key, requestContext.HttpContext.Request.Url,
                        UriComponents.Host, //compare only host here
                        UriFormat.SafeUnescaped, StringComparison.InvariantCultureIgnoreCase) == 0)
                        .OrderByDescending(x => x.Key.AbsolutePath.Length) //have the paths ordered by longest first
                        .ToList();

                    if (hostMatches.Count == 0)
                    {
                        //can't really proceed here so just return the first
                        _logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with a Host value of {HostName} Values: {ArticulateHostValues}", requestContext.HttpContext.Request.Url.Host, DebugHostIdsCollection(DomainsAndIds));
                        return null;
                    }

                    if (hostMatches.Count == 1)
                    {
                        realNodeId = hostMatches[0].Value;
                    }
                    else
                    {
                        var currentPath = requestContext.HttpContext.Request.Url.AbsolutePath;
                        //need to match on paths (longest is first)
                        foreach (var match in hostMatches)
                        {
                            if (match.Key.AbsolutePath.InvariantStartsWith(currentPath))
                            {
                                realNodeId = match.Value;
                                break;
                            }
                        }                        
                    }
                }
            }

            if (realNodeId == 0)
            {
                _logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with a Host value of {HostName}. Values: {ArticulateHostValues}",
                    requestContext.HttpContext.Request.Url.Host,
                    DebugHostIdsCollection(DomainsAndIds));

                return null;
            }

            var byId = umbracoContext.ContentCache.GetById(realNodeId);
            if (byId == null) return null;

            return FindContent(requestContext, umbracoContext, byId);
        }

        private static string DebugHostIdsCollection(IDictionary<Uri, int> hostsAndIds)
        {
            var sb = new StringBuilder();
            foreach (var hostsAndId in hostsAndIds)
            {
                sb.AppendFormat("{0} = {1}, ", hostsAndId.Key, hostsAndId.Value);
            }
            return sb.ToString();
        }

        protected virtual IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext, IPublishedContent baseContent)
        {
            return baseContent;
        }
        
    }
}
