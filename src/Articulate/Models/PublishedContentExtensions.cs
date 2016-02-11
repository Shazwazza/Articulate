using System;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Articulate.Models
{
    public static class PublishedContentExtensions
    {
        public static IPublishedContentWithKey GetContentWithKey(IPublishedContent model)
        {
            var withKey = model as IPublishedContentWithKey;
            if (withKey != null) return withKey;

            var wrapped = model as PublishedContentWrapped;
            if (wrapped != null)
            {
                return GetContentWithKey(wrapped.Unwrap());
            }
            return null;
        }

        public static Guid GetContentKey(this IMasterModel model)
        {
            var withKey = GetContentWithKey(model);
            if (withKey != null) return withKey.Key;
            return Guid.Empty;
        }
    }
}