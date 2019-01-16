using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Mvc;
using Umbraco.Web.Routing;

namespace Articulate
{
    public class ArticulateVirtualNodeByIdRouteHandler : UmbracoVirtualNodeRouteHandler
    {
        private readonly List<Tuple<string, int>> _hostsAndIds = new List<Tuple<string, int>>();
        
        /// <summary>
        /// Constructor used to create a new handler for multi-tenency with domains and ids
        /// </summary>
        /// <param name="umbracoUrlProvider"></param>
        /// <param name="itemsForRoute"></param>
        public ArticulateVirtualNodeByIdRouteHandler(UrlProvider umbracoUrlProvider, IEnumerable<IPublishedContent> itemsForRoute)
        {
            foreach (var publishedContent in itemsForRoute)
            {
                var allUrls = ArticulateRoutes.GetContentUrls(umbracoUrlProvider, publishedContent);

                foreach (var url in allUrls)
                {
                    //if there is a double slash, it will have a domain
                    if (url.Contains("//"))
                    {
                        var uri = new Uri(url, UriKind.Absolute);
                        _hostsAndIds.Add(new Tuple<string, int>(uri.Host, publishedContent.Id));
                    }
                    else
                    {
                        _hostsAndIds.Add(new Tuple<string, int>(string.Empty, publishedContent.Id));
                    }
                }

                Current.Logger.Debug<ArticulateVirtualNodeByIdRouteHandler>("Hosts/IDs map for node {NodeId}. Values: {ArticulateHostValues}", publishedContent.Id, DebugHostIdsCollection());
            }
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
            if (_hostsAndIds.Count == 1)
            {
                realNodeId = _hostsAndIds[0].Item2;
            }
            else
            {
                if (requestContext.HttpContext.Request.Url == null)
                {
                    if (_hostsAndIds.Count > 0)
                    {
                        //cannot be determined
                        realNodeId = _hostsAndIds[0].Item2;
                    }
                    else
                    {
                        Current.Logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found to map hosts and IDs");
                        return null;
                    }
                }
                else if (requestContext.HttpContext.Request.Url.Host.InvariantEquals("localhost")
                    && ! Current.Configs.Settings().RequestHandler.UseDomainPrefixes)
                {
                    //TODO: Why is this checking for UseDomainPrefixes + localhost? I can't figure that part out (even though i wrote that)

                    var found = _hostsAndIds.FirstOrDefault(x => x.Item1 == string.Empty);
                    if (found != null)
                    {
                        realNodeId = found.Item2;
                    }
                    else
                    {
                        Current.Logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with an empty Host value. Values: {ArticulateHostValues}", DebugHostIdsCollection());
                        return null;
                    }
                }
                else
                {
                    var found = _hostsAndIds.FirstOrDefault(x => x.Item1.InvariantEquals(requestContext.HttpContext.Request.Url.Host));
                    if (found != null)
                    {
                        realNodeId = found.Item2;
                    }
                    else
                    {
                        Current.Logger.Warn<ArticulateVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with a Host value of {HostName}. Values: {ArticulateHostValues}",
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
            foreach (var hostsAndId in _hostsAndIds)
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