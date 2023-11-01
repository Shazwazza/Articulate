using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.AspNetCore.Routing;
using System.Collections.Generic;
using Articulate.Controllers;

namespace Articulate.Components
{
    public class ServerVariablesParsingHandler : INotificationHandler<ServerVariablesParsingNotification>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly LinkGenerator _linkGenerator;

        public ServerVariablesParsingHandler(
            IHttpContextAccessor httpContextAccessor,
            LinkGenerator linkGenerator)
        {
            _httpContextAccessor = httpContextAccessor;
            _linkGenerator = linkGenerator;
        }
        public void Handle(ServerVariablesParsingNotification notification)
        {
            var e = notification;

            if (_httpContextAccessor.HttpContext == null)
            {
                throw new InvalidOperationException("HttpContext is null");
            }

            if (e.ServerVariables.ContainsKey(ArticulateConstants.ArticulateContentTypeAlias))
            {
                return;
            }

            e.ServerVariables[ArticulateConstants.ArticulateContentTypeAlias] = new Dictionary<string, object>
            {
                {"articulateImportBaseUrl", _linkGenerator.GetUmbracoApiServiceBaseUrl<ArticulateBlogImportController>(controller => controller.PostImportBlogMl(null))},
                {"articulatePropertyEditorsBaseUrl", _linkGenerator.GetUmbracoApiServiceBaseUrl<ArticulatePropertyEditorsController>(controller => controller.GetThemes())},
                {"articulateThemeEditorBaseUrl", _linkGenerator.GetUmbracoApiServiceBaseUrl<ThemeEditorController>(controller => controller.GetThemes())}
            };
        }
    }
}
