using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Articulate.Syndication;
using Umbraco.Core;
using Umbraco.Web;

namespace Articulate.Options
{
    /// <summary>
    /// Articulate options that affect how articulate works
    /// </summary>
    public class ArticulateOptions
    {
        /// <summary>
        /// Constructor sets defaults
        /// </summary>
        public ArticulateOptions(
            bool autoGenerateExcerpt = true, 
            Func<string, string> generateExcerpt = null,
            Action<MarkdownDeep.Markdown> markdownDeepOptionsCallBack = null)
        {
            AutoGenerateExcerpt = autoGenerateExcerpt;

            GenerateExcerpt = generateExcerpt ?? (val => val == null
                ? string.Empty
                : string.Join("", val.StripHtml()
                    .DecodeHtml()
                    .StripNewLines()
                    .TruncateAtWord(200, "")));
            
            MarkdownDeepOptionsCallBack = markdownDeepOptionsCallBack ?? (markdown => { });
        }

        internal static ArticulateOptions Default = new ArticulateOptions();

        /// <summary>
        /// Can be set at startup to specify alternate options
        /// </summary>
        public static void Setup(ArticulateOptions options)
        {
            Default = options;
        }

        /// <summary>
        /// Returns the Rss feed generator instance that will be used to create feeds
        /// </summary>
        public virtual IRssFeedGenerator GetRssFeedGenerator(UmbracoContext umbracoContext)
        {
            return new RssFeedGenerator(umbracoContext);
        }

        /// <summary>
        /// Default is true and will generate an excerpt if it is blank, will be a truncated version based on the post content
        /// </summary>
        public bool AutoGenerateExcerpt { get; private set; }

        /// <summary>
        /// The default generator will truncate the post content with 200 chars
        /// </summary>
        public Func<string, string> GenerateExcerpt { get; private set; }

        /// <summary>
        /// The default formatter does nothing
        /// </summary>
        public Action<MarkdownDeep.Markdown> MarkdownDeepOptionsCallBack { get; private set; }
    }
}
