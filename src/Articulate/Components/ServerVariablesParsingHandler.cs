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
                //{"articulateDataInstallerBaseUrl", _linkGenerator.GetUmbracoApiServiceBaseUrl<ArticulateBlogDataInstallController>(controller => controller.PostInstall())},
                {"articulatePropertyEditorsBaseUrl", _linkGenerator.GetUmbracoApiServiceBaseUrl<ArticulatePropertyEditorsController>(controller => controller.GetThemes())}
            };

            if (e.ServerVariables.TryGetValue("umbracoUrls", out object found))
            {
                var umbUrls = (Dictionary<string, object>)found;
                umbUrls["articulateThemeEditorApiBaseUrl"] = _linkGenerator.GetUmbracoApiServiceBaseUrl<ThemeEditorController>(controller => controller.GetByPath(null));
            }
            else
            {
                e.ServerVariables["umbracoUrls"] = new Dictionary<string, object>
                {
                    {"articulateThemeEditorApiBaseUrl", _linkGenerator.GetUmbracoApiServiceBaseUrl<ThemeEditorController>(controller => controller.GetByPath(null))}
                };
            }
        }
    }
}
