using System.Collections.Generic;
using System.ServiceModel.Syndication;
using Articulate.Models;

namespace Articulate.Syndication
{
    public interface IRssFeedGenerator
    {
        SyndicationFeed GetFeed(IMasterModel rootPageModel, IEnumerable<PostModel> posts);
    }
}
