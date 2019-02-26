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
        private readonly Dictionary<string, int> _hostsAndIds = null;
        private readonly ILogger _logger;
        private readonly Lazy<Dictionary<string, int>> _lazyHostsAndIds;

        private IDictionary<string, int> HostsAndIds => _hostsAndIds ?? _lazyHostsAndIds.Value;

        /// <summary>
        /// Constructor used to create a new handler for multi-tenency with domains and ids
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="contentUrls"></param>
        /// <param name="itemsForRoute"></param>
        public ArticulateVirtualNodeByIdRouteHandler(ILogger logger, ContentUrls contentUrls, IEnumerable<IPublishedContent> itemsForRoute)
        {
            _logger = logger;
            _lazyHostsAndIds = new Lazy<Dictionary<string, int>>(() =>
            {
                //make this lazy so we can reduce the startup overhead

                var hostsAndIds = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
                foreach (var publishedContent in itemsForRoute)
                {
                    var allUrls = contentUrls.GetContentUrls(publishedContent);

                    foreach (var url in allUrls)
                    {
                        //if there is a double slash, it will have a domain
                        if (url.Contains("//"))
                        {
                            var uri = new Uri(url, UriKind.Absolute);
                            hostsAndIds[uri.Host] = publishedContent.Id;
                        }
                        else
                        {
                            hostsAndIds[string.Empty] = publishedContent.Id;
                        }
                    }

                    _logger.Debug<ArticulateVirtualNodeByIdRouteHandler>("Hosts/IDs map for node {NodeId}. Values: {ArticulateHostValues}", publishedContent.Id, DebugHostIdsCollection(hostsAndIds));
                    
                }

                return hostsAndIds;
            });

            
        }

        /// <summary>
        /// Constructor used to create a new handler for only one id and no domain
        /// </summary>
        /// <param name="realNodeId"></param>
        public ArticulateVirtualNodeByIdRouteHandler(int realNodeId)
        {
            _hostsAndIds = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase) {[string.Empty] = realNodeId};
        }

        protected sealed override IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext)
        {
            //determine if it's for a particular domain
            int realNodeId;
            if (HostsAndIds.Count == 1)
            {
                realNodeId = HostsAndIds.Values.First();
            }
            else
            {
                if (requestContext.HttpContext.Request.Url == null)
                {
                    if (HostsAndIds.Count > 0)
                    {
                        //cannot be determined
                        realNodeId = HostsAndIds.Values.First();
                    }
                    else
                    {
                        _logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found to map hosts and IDs");
                        return null;
                    }
                }
                else if (requestContext.HttpContext.Request.Url.Host.InvariantEquals("localhost"))
                {
                    if (HostsAndIds.TryGetValue(string.Empty, out var val))
                    {
                        realNodeId = val;
                    }
                    else
                    {
                        _logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with an empty Host value. Values: {ArticulateHostValues}", DebugHostIdsCollection(HostsAndIds));
                        return null;
                    }
                }
                else
                {
                    if (HostsAndIds.TryGetValue(requestContext.HttpContext.Request.Url.Host, out var val))
                    {
                        realNodeId = val;
                    }
                    else
                    {
                        _logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with a Host value of {HostName}. Values: {ArticulateHostValues}",
                            requestContext.HttpContext.Request.Url.Host,
                            DebugHostIdsCollection(HostsAndIds));

                        return null;
                    }
                }
            }

            var byId = umbracoContext.ContentCache.GetById(realNodeId);
            if (byId == null) return null;

            return FindContent(requestContext, umbracoContext, byId);
        }

        private static string DebugHostIdsCollection(IDictionary<string, int> hostsAndIds)
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