using System;
using System.Collections;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Articulate.Models
{
    public static class PublishedContentExtensions
    {
        /// <summary>
        /// Returns true if there is more than x items
        /// </summary>
        /// <param name="source"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <summary>
        /// Returns true if source has at least <paramref name="count"/> elements efficiently.
        /// </summary>
        /// <remarks>Based on int Enumerable.Count() method.</remarks>
        public static bool HasMoreThan<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var collection = source as ICollection<TSource>;
            if (collection != null)
            {
                return collection.Count > count;
            }
            var collection2 = source as ICollection;
            if (collection2 != null)
            {
                return collection2.Count > count;
            }
            int num = 0;
            checked
            {
                using (var enumerator = source.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        num++;
                        if (num > count)
                        {
                            return true;
                        }
                    }
                }
            }
            return false; // < count
        }

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