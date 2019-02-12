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
            composition.UrlProviders().Append<VirtualNodeUrlProvider>();
            composition.UrlProviders().InsertBefore<DefaultUrlProvider, DateFormattedUrlProvider>();
            
            composition.ContentFinders().InsertBefore<ContentFinderByUrl, DateFormattedPostContentFinder>();

            composition.Configs.Add<ArticulateOptions>(_ => ArticulateOptions.Default);

            composition.Components().Append<ArticulateComponent>();
        }
    }
}
