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

namespace Articulate
{
    public class ArticulateVirtualNodeByIdRouteHandler : UmbracoVirtualNodeRouteHandler
    {
        private readonly List<Tuple<string, int>> _hostsAndIds = new List<Tuple<string, int>>();
        private readonly ILogger _logger;
        private readonly Lazy<List<Tuple<string, int>>> _lazyHostsAndIds;

        private List<Tuple<string, int>> HostsAndIds => _hostsAndIds ?? _lazyHostsAndIds.Value;

        /// <summary>
        /// Constructor used to create a new handler for multi-tenency with domains and ids
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="contentUrls"></param>
        /// <param name="itemsForRoute"></param>
        public ArticulateVirtualNodeByIdRouteHandler(ILogger logger, ContentUrls contentUrls, IEnumerable<IPublishedContent> itemsForRoute)
        {
            _logger = logger;
            _lazyHostsAndIds = new Lazy<List<Tuple<string, int>>>(() =>
            {
                //needs to be lazy because we can only collect the hosts/ids when there's an umbraco context

                var hostsAndIds = new List<Tuple<string, int>>();
                foreach (var publishedContent in itemsForRoute)
                {
                    var allUrls = contentUrls.GetContentUrls(publishedContent);

                    foreach (var url in allUrls)
                    {
                        //if there is a double slash, it will have a domain
                        if (url.Contains("//"))
                        {
                            var uri = new Uri(url, UriKind.Absolute);
                            hostsAndIds.Add(new Tuple<string, int>(uri.Host, publishedContent.Id));
                        }
                        else
                        {
                            hostsAndIds.Add(new Tuple<string, int>(string.Empty, publishedContent.Id));
                        }
                    }

                    _logger.Debug<ArticulateVirtualNodeByIdRouteHandler>("Hosts/IDs map for node {NodeId}. Values: {ArticulateHostValues}", publishedContent.Id, DebugHostIdsCollection());
                    
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
            _hostsAndIds.Add(new Tuple<string, int>(string.Empty, realNodeId));
        }

        protected sealed override IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext)
        {
            //determine if it's for a particular domain
            int realNodeId;
            if (HostsAndIds.Count == 1)
            {
                realNodeId = HostsAndIds[0].Item2;
            }
            else
            {
                if (requestContext.HttpContext.Request.Url == null)
                {
                    if (HostsAndIds.Count > 0)
                    {
                        //cannot be determined
                        realNodeId = HostsAndIds[0].Item2;
                    }
                    else
                    {
                        _logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found to map hosts and IDs");
                        return null;
                    }
                }
                else if (requestContext.HttpContext.Request.Url.Host.InvariantEquals("localhost"))
                {
                    var found = HostsAndIds.FirstOrDefault(x => x.Item1 == string.Empty);
                    if (found != null)
                    {
                        realNodeId = found.Item2;
                    }
                    else
                    {
                        _logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with an empty Host value. Values: {ArticulateHostValues}", DebugHostIdsCollection());
                        return null;
                    }
                }
                else
                {
                    var found = HostsAndIds.FirstOrDefault(x => x.Item1.InvariantEquals(requestContext.HttpContext.Request.Url.Host));
                    if (found != null)
                    {
                        realNodeId = found.Item2;
                    }
                    else
                    {
                        _logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with a Host value of {HostName}. Values: {ArticulateHostValues}",
                            requestContext.HttpContext.Request.Url.Host,
                            DebugHostIdsCollection());

                        return null;
                    }
                }
            }

            var byId = umbracoContext.ContentCache.GetById(realNodeId);
            if (byId == null) return null;

            return FindContent(requestContext, umbracoContext, byId);
        }

        private string DebugHostIdsCollection()
        {
            var sb = new StringBuilder();
            foreach (var hostsAndId in HostsAndIds)
            {
                sb.AppendFormat("{0} = {1}, ", hostsAndId.Item1, hostsAndId.Item2);
            }
            return sb.ToString();
        }

        protected virtual IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext, IPublishedContent baseContent)
        {
            return baseContent;
        }
        
    }
}