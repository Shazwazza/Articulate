using System;
using Articulate.Routing;

namespace Articulate.Components
{
    public class MetaWeblogHandlerFactory
    {
        private readonly Func<int, MetaWeblogHandler> _factory;

        public MetaWeblogHandlerFactory(Func<int, MetaWeblogHandler> factory)
        {
            _factory = factory;
        }

        public MetaWeblogHandler Create(int contentId) => _factory(contentId);
    }
}