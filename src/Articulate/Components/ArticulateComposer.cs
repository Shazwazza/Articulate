using Articulate.ImportExport;
using Articulate.MetaWeblog;
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
using WilderMinds.MetaWeblog;

namespace Articulate.Components
{

    public class ArticulateComposer : ComponentComposer<ArticulateComponent>
    {
        public override void Compose(IUmbracoBuilder builder)
        {
            base.Compose(builder);

            var services = builder.Services;
            //services.AddSingleton<ArticulateRoutes>();
            services.AddSingleton<ContentUrls>();
            services.AddSingleton<ArticulateDataInstaller>();
            //services.AddSingleton<ArticulateTempFileSystem>(x => new ArticulateTempFileSystem("~/App_Data/Temp/Articulate"));
            services.AddSingleton<BlogMlExporter>();
            services.AddSingleton<IRssFeedGenerator, RssFeedGenerator>();

            services.AddSingleton<IArticulateTagRepository, ArticulateTagRepository>();
            services.AddSingleton<ArticulateTagService>();

            services.AddSingleton<DisqusXmlExporter>();
            services.AddSingleton<BlogMlImporter>();
            services.AddSingleton<CacheRefreshKey>();
            services.AddSingleton<IArticulateSearcher, DefaultArticulateSearcher>();
            services.AddSingleton<ArticulateRouteValueTransformer>();
            services.AddSingleton<ArticulateRouter>();
            services.AddSingleton<RouteCacheRefresherMiddleware>();

            //services.TryAddEnumerable(Singleton<MatcherPolicy, ArticulateSelectorPolicy>());
            //services.AddSingleton<ArticulateEndpointDataSource>();           

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

            builder.AddNotificationHandler<ContentPublishedNotification, ContentPublishedHandler>();
            builder.AddNotificationHandler<ContentUnpublishedNotification, ContentUnpublishedHandler>();

            builder.Services.ConfigureOptions<ArticulatePipelineStartupFilter>();
        }
    }
}
