using System;
using System.Collections;
using System.Collections.Generic;

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
               
    }
}