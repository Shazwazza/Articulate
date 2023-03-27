using System;
using Articulate.ImportExport;
using Articulate.Options;
using Articulate.Packaging;
using Articulate.Routing;
using Articulate.Services;
using Articulate.Syndication;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Routing;

namespace Articulate.Components
{

    public class ArticulateComposer : ComponentComposer<ArticulateComponent>
    {
        public override void Compose(IUmbracoBuilder builder)
        {
            base.Compose(builder);

            var services = builder.Services;
            services.AddSingleton<ContentUrls>();
            services.AddSingleton<BlogMlExporter>();
            services.AddSingleton<ArticulateTempFileSystem>();
            services.AddSingleton<IRssFeedGenerator, RssFeedGenerator>();

            services.AddSingleton<IArticulateTagRepository, ArticulateTagRepository>();
            services.AddSingleton<ArticulateTagService>();

            services.AddSingleton<DisqusXmlExporter>();
            services.AddSingleton<BlogMlImporter>();
            services.AddSingleton<IArticulateSearcher, DefaultArticulateSearcher>();
            services.AddSingleton<ArticulateRouteValueTransformer>();
            services.AddSingleton<ArticulateRouter>();
            services.AddSingleton<RouteCacheRefresherFilter>();
            services.AddSingleton<ArticulateFrontEndFilterConvention>();

            builder.UrlProviders().Append<VirtualNodeUrlProvider>();
            builder.UrlProviders().InsertBefore<DefaultUrlProvider, DateFormattedUrlProvider>();

            builder.ContentFinders().InsertBefore<ContentFinderByUrl, DateFormattedPostContentFinder>();

            services.AddOptions<ArticulateOptions>();

            builder.AddNotificationHandler<ContentSavingNotification, ContentSavingHandler>();
            builder.AddNotificationHandler<ContentSavedNotification, ContentSavedHandler>();
            builder.AddNotificationHandler<ContentTypeSavingNotification, ContentTypeSavingHandler>();
            builder.AddNotificationHandler<ServerVariablesParsingNotification, ServerVariablesParsingHandler>();
            builder.AddNotificationHandler<ContentCacheRefresherNotification, ContentCacheRefresherHandler>();
            builder.AddNotificationHandler<DomainCacheRefresherNotification, DomainCacheRefresherHandler>();
            builder.AddNotificationHandler<SendingContentNotification, SendingContentHandler>();

            builder.Services.ConfigureOptions<ArticulatePipelineStartupFilter>();
            builder.Services.ConfigureOptions<ConfigureArticulateMvcOptions>();

#if NET7_0_OR_GREATER
            builder.Services.AddOutputCache(options =>
            {               
                options.AddPolicy("Articulate120", builder =>
                    builder.Expire(TimeSpan.FromSeconds(120)));
                options.AddPolicy("Articulate300", builder =>
                    builder.Expire(TimeSpan.FromSeconds(300)));
                options.AddPolicy("Articulate60", builder =>
                    builder.Expire(TimeSpan.FromSeconds(60)));
            });
#endif
        }
    }
}
