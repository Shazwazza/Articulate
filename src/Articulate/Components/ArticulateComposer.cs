#nullable enable
using Articulate.Controllers;
using Articulate.ImportExport;
using Articulate.Options;
using Articulate.Routing;
using Articulate.Services;
using Articulate.Syndication;
using FileSignatures;
using FileSignatures.Formats;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Routing;

namespace Articulate.Components
{
    /// <summary>
    /// Composer for Articulate dependencies and configuration.
    /// </summary>
    public class ArticulateComposer : IComposer
    {
        /// <inheritdoc/>
        public void Compose(IUmbracoBuilder builder)
        {
            IServiceCollection services = builder.Services;
            _ = services.AddScoped<BlogMlExporter>();
            _ = services.AddSingleton<ArticulateTempFileSystem>();
            services.TryAddSingleton<IRssFeedGenerator, RssFeedGenerator>();
            services.TryAddSingleton<IArticulateRouteRefreshState, ArticulateRouteRefreshState>();

            services.TryAddSingleton<IArticulateTagRepository, ArticulateTagRepository>();
            _ = services.AddSingleton<ArticulateTagService>();
            _ = services.AddSingleton<IFileFormatInspector>(_ =>
                new FileFormatInspector([new Jpeg(), new Png(), new Gif()]));
            _ = services.AddScoped<IArticulateImportMediaService, ArticulateImportMediaService>();

            _ = services.AddSingleton<DisqusXmlExporter>();
            _ = services.AddScoped<BlogMlImporter>();
            services.TryAddSingleton<IArticulateSearcher, DefaultArticulateSearcher>();
            _ = services.AddSingleton<ArticulateRouteValueTransformer>();
            _ = services.AddSingleton<ArticulateRouter>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<MatcherPolicy, ArticulateDynamicRouteSelectorPolicy>());
            services.TryAddSingleton<IArticulateThemeRepository, ArticulateThemeRepository>();
            services.TryAddSingleton<IArticulateMarkdownConverter, ArticulateMarkdownService>();
            _ = services.AddTransient<IArticulateThemeResolver, ArticulateThemeResolver>();
            _ = services.AddScoped<BackOfficeAuthService>();

            // Register DI-driven view location provider and configure Razor view engine with provider
            services.TryAddSingleton<IArticulateViewLocationProvider, DefaultArticulateViewLocationProvider>();
            _ = services.Configure<RazorViewEngineOptions>(options =>
            {
                options.ViewLocationExpanders.Add(new ArticulateViewLocationExpander());
            });

#if NET10_0_OR_GREATER && UMBRACO_18_OR_GREATER
            _ = builder.UrlProviders().InsertBefore<DefaultUrlProvider, DateFormattedUrlProvider>();
            _ = builder.ContentFinders().InsertBefore<ContentFinderByUrl, DateFormattedPostContentFinder>();
#else
            _ = builder.UrlProviders().InsertBefore<NewDefaultUrlProvider, DateFormattedUrlProvider>();
            _ = builder.ContentFinders().InsertBefore<ContentFinderByUrlNew, DateFormattedPostContentFinder>();
#endif

            _ = services.AddOptions<ArticulateOptions>()
                .BindConfiguration("Articulate");

            _ = builder.AddNotificationHandler<ContentSavingNotification, ContentSavingHandler>();
            _ = builder.AddNotificationHandler<ContentPublishingNotification, ContentPublishingHandler>();
            _ = builder.AddNotificationAsyncHandler<ContentSavedNotification, ArticulateRootContentLifecycleHandler>();
            _ = builder.AddNotificationAsyncHandler<ContentPublishedNotification, ArticulateRootContentLifecycleHandler>();
            _ = builder.AddNotificationHandler<ContentTypeSavingNotification, ContentTypeSavingHandler>();
            _ = builder.AddNotificationHandler<ContentCacheRefresherNotification, ContentCacheRefresherHandler>();
            _ = builder.AddNotificationHandler<DomainCacheRefresherNotification, DomainCacheRefresherHandler>();

            // Ensure MVC discovers controllers in the Articulate assembly.
            _ = services
                .AddControllersWithViews()
                .ConfigureApplicationPartManager(apm =>
                {
                    System.Reflection.Assembly controllersAssembly = typeof(ArticulateController).Assembly;
                    if (apm.ApplicationParts.OfType<AssemblyPart>().All(p => p.Assembly != controllersAssembly))
                    {
                        apm.ApplicationParts.Add(new AssemblyPart(controllersAssembly));
                    }
                });

            _ = services.ConfigureOptions<ArticulatePipelineStartupFilter>();

            _ = services.AddOutputCache(options =>
            {
                options.AddPolicy("Articulate120", policyBuilder =>
                    policyBuilder
                        .Expire(TimeSpan.FromSeconds(120)));
                options.AddPolicy("Articulate300", policyBuilder =>
                    policyBuilder
                        .Expire(TimeSpan.FromSeconds(300)));
                options.AddPolicy("Articulate60", policyBuilder =>
                    policyBuilder
                        .Expire(TimeSpan.FromSeconds(60)));
            });
        }
    }
}
