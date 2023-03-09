using Markdig;

namespace Articulate
{
    public static class MarkdownHelper
    {
        private static readonly MarkdownPipeline s_markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static string ToHtml(string input) => Markdown.ToHtml(input, s_markdownPipeline);
    }
}
