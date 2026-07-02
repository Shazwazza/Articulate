#nullable enable
using Umbraco.Cms.Core.Models;

namespace Articulate.Services
{
    public interface IArticulateRichTextRenderer
    {
        public string GetMarkup(string? value);
        public string GetRenderedMarkup(IContent content, string propertyAlias = "richText");
    }
}
