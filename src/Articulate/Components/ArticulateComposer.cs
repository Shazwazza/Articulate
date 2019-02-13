using Articulate.Options;
using Umbraco.Core;
using Umbraco.Core.Components;
using Umbraco.Core.Composing;
using Umbraco.Core.IO;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate.Components
{
    [RuntimeLevel(MinLevel = RuntimeLevel.Run)]
    public class ArticulateComposer : IUserComposer
    {
        public void Compose(Composition composition)
        {
            composition.RegisterUnique<ArticulateDataInstaller>();
            composition.RegisterUnique<ArticulateTempFileSystem>(x => new ArticulateTempFileSystem("~/App_Data/Temp/Articulate"));
            
            composition.Register<DisqusXmlExporter>(Lifetime.Request);
            composition.Register<BlogMlImporter>(Lifetime.Request);
            composition.Register<IArticulateSearcher, DefaultArticulateSearcher>(Lifetime.Request);


            composition.UrlProviders().Append<VirtualNodeUrlProvider>();
            composition.UrlProviders().InsertBefore<DefaultUrlProvider, DateFormattedUrlProvider>();
            
            composition.ContentFinders().InsertBefore<ContentFinderByUrl, DateFormattedPostContentFinder>();

            composition.Configs.Add<ArticulateOptions>(_ => ArticulateOptions.Default);

            composition.Components().Append<ArticulateComponent>();
        }
    }
}
