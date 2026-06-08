using ArticulateDockerSite.Options;
using ArticulateDockerSite.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Umbraco.Cms.Core.Notifications;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers for reverse proxy (Caddy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    // Trust proxies from Docker network
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

IUmbracoBuilder umbBuilder = builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers();

_ = umbBuilder.Services.AddOptions<ArticulateDevAutomationOptions>()
    .BindConfiguration(ArticulateDevAutomationOptions.SectionName);
_ = umbBuilder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, ArticulateDevAutomationBootstrapper>();

umbBuilder.Build();

// Increase upload limits, e.g. importing larger BlogML XML files; also ensure Umbraco:CMS:Runtime:MaxRequestLength is set
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100MB
});
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100MB
});

WebApplication app = builder.Build();

// IMPORTANT: UseForwardedHeaders must be called before other middleware
app.UseForwardedHeaders();

await app.BootUmbracoAsync().ConfigureAwait(false);

if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseUmbracoPreviewEndpoints();
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync().ConfigureAwait(false);
