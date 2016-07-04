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
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    public class UmbracoVirtualNodeByIdRouteHandler : UmbracoVirtualNodeRouteHandler
    {
        private readonly List<Tuple<string, int>> _hostsAndIds = new List<Tuple<string, int>>();

        [Obsolete("Use the ctor with all dependencies instead")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public UmbracoVirtualNodeByIdRouteHandler(IEnumerable<IPublishedContent> itemsForRoute)
            : this(UmbracoContext.Current.UrlProvider, itemsForRoute)
        {
        }

        /// <summary>
        /// Constructor used to create a new handler for multi-tenency with domains and ids
        /// </summary>
        /// <param name="umbracoUrlProvider"></param>
        /// <param name="itemsForRoute"></param>
        public UmbracoVirtualNodeByIdRouteHandler(UrlProvider umbracoUrlProvider, IEnumerable<IPublishedContent> itemsForRoute)
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
                LogHelper.Debug<UmbracoVirtualNodeByIdRouteHandler>(() => $"Hosts/IDs map for node {publishedContent.Id}. Values: {DebugHostIdsCollection()}");
            }
        }

        /// <summary>
        /// Constructor used to create a new handler for only one id and no domain
        /// </summary>
        /// <param name="realNodeId"></param>
        public UmbracoVirtualNodeByIdRouteHandler(int realNodeId)
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
                        LogHelper.Warn<UmbracoVirtualNodeByIdRouteHandler>("No entries found to map hosts and IDs");
                        return null;
                    }
                }
                else if (requestContext.HttpContext.Request.Url.Host.InvariantEquals("localhost") 
                    && !UmbracoConfig.For.UmbracoSettings().RequestHandler.UseDomainPrefixes)
                {
                    //TODO: Why is this checking for UseDomainPrefixes + localhost? I can't figure that part out (even though i wrote that)

                    var found = _hostsAndIds.FirstOrDefault(x => x.Item1 == string.Empty);
                    if (found != null)
                    {
                        realNodeId = found.Item2;
                    }
                    else
                    {
                        LogHelper.Warn<UmbracoVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with an empty Host value. Values: " + DebugHostIdsCollection());
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
                        LogHelper.Warn<UmbracoVirtualNodeByIdRouteHandler>("No entries found in hosts/IDs map with a Host value of " + requestContext.HttpContext.Request.Url.Host + ". Values: " + DebugHostIdsCollection());
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

        //NOTE: This is the manual way we could assign culture this but I think there's more logic for edge case scenarios in Umbraco's Prepare method.
        // I've just left this code here as an example
        protected override void PreparePublishedContentRequest(PublishedContentRequest publishedContentRequest)
        {
            //if (_hostsAndIds.Any(x => x.Item2 == publishedContentRequest.PublishedContent.Parent.Id))
            //{
            //    var hostAndId = _hostsAndIds.Single(x => x.Item2 == publishedContentRequest.PublishedContent.Parent.Id);
            //    var domain = Domain.GetDomain(hostAndId.Item1);
            //    publishedContentRequest.Culture = new CultureInfo(domain.Language.CultureAlias);
            //}

            base.PreparePublishedContentRequest(publishedContentRequest);          
            
        }
    }
}