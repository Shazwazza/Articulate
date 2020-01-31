using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web.HealthCheck;

namespace Articulate.HealthChecks
{
    [HealthCheck(
        "9A1B3405-E743-40EC-B45D-4C5A6F8B093F",
        "Tags Checkup",
        Description = "Checks if there are out of sync tags in content tag data and data stored in the tags database tables. This is an Expensive operation!",
        Group = "Services")]
    public class TagHealthCheck : HealthCheck
    {
        private readonly ILocalizedTextService _textService;
        private readonly IContentService _contentService;
        private readonly IContentTypeService _contentTypeService;
        private readonly ITagService _tagService;

        public TagHealthCheck(ILocalizedTextService textService, IContentService contentService, IContentTypeService contentTypeService, ITagService tagService)
        {
            _textService = textService;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _tagService = tagService;
        }

        public override HealthCheckStatus ExecuteAction(HealthCheckAction action)
        {
            return new HealthCheckStatus("Tags are outta whack!")
            {
                ResultType = StatusResultType.Success
            };
        }

        public override IEnumerable<HealthCheckStatus> GetStatus()
        {
            var groupedContentTypes = _contentTypeService.GetAll()
                .ToDictionary(
                    x => x.Id,
                    x => (contentType: x, tagPropertyTypes: x.CompositionPropertyTypes.Where(p => p.PropertyEditorAlias == Umbraco.Core.Constants.PropertyEditors.Aliases.Tags).ToList()));


            var contentWithTagEditors = new List<IContent>();
            var pageSize = 200;
            var pageIndex = 0;
            var withTagsPropertyEditors = groupedContentTypes.Where(x => x.Value.tagPropertyTypes.Count > 0).Select(x => x.Key).ToArray();

            var inconsistencies = new HashSet<int>();

            do
            {
                contentWithTagEditors = _contentService.GetPagedOfTypes(
                    withTagsPropertyEditors,
                    pageIndex, pageSize, out var totalContent, null).ToList();

                foreach (var content in contentWithTagEditors)
                {
                    var ct = groupedContentTypes[content.ContentTypeId];
                    foreach (var tagProp in ct.tagPropertyTypes)
                    {
                        var tagVal = content.GetValue<string>(tagProp.Alias, published: true);
                        if (!tagVal.IsNullOrWhiteSpace())
                        {
                            var tags = tagVal.DetectIsJson()
                                ? JsonConvert.DeserializeObject<string[]>(tagVal)
                                : tagVal.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                            // TODO: N+1!!

                            var dbTags = _tagService.GetTagsForEntity(content.Key)
                                .Select(x => x.Text)
                                .ToList();

                            foreach (var tag in tags)
                            {
                                if (!dbTags.InvariantContains(tag))
                                {
                                    // This means there's more tags in the text than in the DB
                                    inconsistencies.Add(content.Id);
                                    // TODO: How to deal with this?
                                }
                            }

                            foreach (var dbTag in dbTags)
                            {
                                if (!tags.InvariantContains(dbTag))
                                {
                                    // Odd, this means there's more tags in the DB than in the text!?
                                    inconsistencies.Add(content.Id);
                                    // TODO: How to deal with this?
                                }
                            }

                        }
                    }
                }

                pageIndex++;
            }
            while (contentWithTagEditors.Count >= pageSize);

            IEnumerable<HealthCheckAction> actions = new List<HealthCheckAction>
            {
                new HealthCheckAction("rectify", Id)
                {
                    Name = _textService.Localize("healthcheck/rectifyButton"),
                    Description = $"There are {inconsistencies.Count} documents that have inconsistent tag data"
                }
            };

            yield return new HealthCheckStatus("Tags are outta whack!")
            {
                ResultType = inconsistencies.Count == 0 ? StatusResultType.Success : StatusResultType.Error,
                Actions = inconsistencies.Count == 0 ? Enumerable.Empty<HealthCheckAction>() : actions
            };
        }
    }
}
