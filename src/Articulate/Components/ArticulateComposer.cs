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

    public class ArticulateComposer : IUserComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            var services = builder.Services;
            services.AddSingleton<ArticulateRoutes>();
            services.AddSingleton<ContentUrls>();
            services.AddSingleton<ArticulateDataInstaller>();
            services.AddSingleton<ArticulateTempFileSystem>(x => new ArticulateTempFileSystem("~/App_Data/Temp/Articulate"));
            services.AddSingleton<BlogMlExporter>();
            services.AddSingleton<IRssFeedGenerator, RssFeedGenerator>();

            services.AddSingleton<IArticulateTagRepository, ArticulateTagRepository>();
            services.AddSingleton<ArticulateTagService>();

            services.AddScoped<DisqusXmlExporter>();
            services.AddScoped<BlogMlImporter>();

            services.AddScoped<IArticulateSearcher, DefaultArticulateSearcher>();

            services.AddTransient<MetaWeblogHandler>();
            services.AddSingleton<MetaWeblogHandlerFactory>(factory => new MetaWeblogHandlerFactory(i =>
           {
               var instance = factory.GetRequiredService<MetaWeblogHandler>();
               instance.BlogRootId = i;
               return instance;
           }));

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

        }

    }
}
