#nullable enable
using Markdig;
using Umbraco.Cms.Core.Security;

namespace Articulate.Services
{
    /// <summary>
    ///     Service for converting Markdown to HTML with sanitization.
    /// </summary>
    public class ArticulateMarkdownService(IHtmlSanitizer htmlSanitizer) : IArticulateMarkdownConverter
    {
        private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        /// <inheritdoc />
        public string ToHtml(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return string.Empty;
            }

            var html = Markdown.ToHtml(markdown, _markdownPipeline);
            return htmlSanitizer.Sanitize(html);
        }
    }
}
