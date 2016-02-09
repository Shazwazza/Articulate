using System;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Articulate.Models
{
    public static class PublishedContentExtensions
    {
        public static Guid GetContentKey(this IMasterModel model)
        {
            var withKey = model as IPublishedContentWithKey;
            if (withKey != null) return withKey.Key;
            
            var wrapped = model as PublishedContentWrapped;
            if (wrapped != null)
            {
                withKey = wrapped.Unwrap() as IPublishedContentWithKey;
                if (withKey != null) return withKey.Key;
            }
            return Guid.Empty;
        }
    }
}