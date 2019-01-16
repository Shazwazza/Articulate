using Articulate.Options;
using Umbraco.Core;
using Umbraco.Core.Components;
using Umbraco.Web.Routing;

namespace Articulate.Components
{
    [RuntimeLevel(MinLevel = RuntimeLevel.Run)]
    public class ArticulateComposer : IUserComposer
    {
        public void Compose(Composition composition)
        {
            //TODO Register these - composition.UrlProviders is internal need a V8 build
            //UrlProviderResolver.Current.AddType<VirtualNodeUrlProvider>();
            //UrlProviderResolver.Current.InsertTypeBefore<DefaultUrlProvider, DateFormattedUrlProvider>();
            
            composition.ContentFinders().InsertBefore<ContentFinderByUrl, DateFormattedPostContentFinder>();

            composition.Configs.Add<ArticulateOptions>(_ => ArticulateOptions.Default);

            composition.Components().Append<ArticulateComponent>();
        }
    }
}
