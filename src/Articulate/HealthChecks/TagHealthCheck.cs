using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Scoping;
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
        private readonly ILocalizationService _languageService;
        private readonly IScopeProvider _scopeProvider;
        private readonly ITagRepository _tagRepository;

        public TagHealthCheck(ILocalizedTextService textService, IContentService contentService, IContentTypeService contentTypeService, ITagService tagService,
            ILocalizationService languageService, IScopeProvider scopeProvider,
            // This is generally bad practice! but we have no choice since the services don't expose anything for us
            ITagRepository tagRepository)
        {
            _textService = textService;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _tagService = tagService;
            _languageService = languageService;
            _scopeProvider = scopeProvider;
            _tagRepository = tagRepository;
        }

        public override HealthCheckStatus ExecuteAction(HealthCheckAction action)
        {
            if (action.ActionParameters.TryGetValue("contentIds", out var ids) && ids is JObject contentIdsObj)
            {
                using (var scope = _scopeProvider.CreateScope())
                {
                    var contentIds = contentIdsObj.ToObject<Dictionary<int, string[]>>();

                    var contentItems = _contentService.GetByIds(contentIds.Keys).ToList();

                    foreach (var content in contentItems)
                    {
                        // TODO: Use SetEntityTags

                        //var tagProps = contentIds[content.Id];
                        //foreach (var tagProp in tagProps)
                        //{
                        //    var tagVal = content.GetValue<string>(tagProp, published: true);
                        //    if (!tagVal.IsNullOrWhiteSpace())
                        //    {
                        //        var tags = tagVal.DetectIsJson()
                        //            ? JsonConvert.DeserializeObject<string[]>(tagVal)
                        //            : tagVal.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                        //    }
                        //}
                    }

                    scope.Complete();
                }
            }

            return new HealthCheckStatus("Tags are outta whack!")
            {
                ResultType = StatusResultType.Success
            };
        }


        // Taken from Umbraco ... we need to basically do this, prob need reflection, etc...
        private void SetEntityTags(IContentBase entity, ITagRepository tagRepo)
        {
            foreach (var property in entity.Properties)
            {
                // reflection
                var tagConfiguration = (TagConfiguration)typeof(PropertyTagsExtensions).CallStaticMethod("GetTagConfiguration", property);
                if (tagConfiguration == null) continue; // not a tags property

                if (property.PropertyType.VariesByCulture())
                {
                    var tags = new List<ITag>();
                    foreach (var pvalue in property.Values)
                    {
                        // reflection
                        var tagsValue = (IEnumerable<string>)typeof(PropertyTagsExtensions).CallStaticMethod("GetTagsValue", property, pvalue.Culture);
                        var languageId = _languageService.GetLanguageIdByIsoCode(pvalue.Culture);
                        var cultureTags = tagsValue.Select(x => new Tag { Group = tagConfiguration.Group, Text = x, LanguageId = languageId });
                        tags.AddRange(cultureTags);
                    }
                    tagRepo.Assign(entity.Id, property.PropertyType.Id, tags);
                }
                else
                {
                    var tagsValue = (IEnumerable<string>)typeof(PropertyTagsExtensions).CallStaticMethod("GetTagsValue", property, null);
                    var tags = tagsValue.Select(x => new Tag { Group = tagConfiguration.Group, Text = x });
                    tagRepo.Assign(entity.Id, property.PropertyType.Id, tags);
                }
            }
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

            var inconsistencies = new Dictionary<int, HashSet<string>>();

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
                            // TODO: use GetTagsValue with reflection
                            // TODO: What about culture?

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
                                    if (inconsistencies.TryGetValue(content.Id, out var propAliases))
                                        propAliases.Add(tagProp.Alias);
                                    else
                                        inconsistencies.Add(content.Id, new HashSet<string> { tagProp.Alias });
                                    // TODO: How to deal with this?
                                }
                            }

                            foreach (var dbTag in dbTags)
                            {
                                if (!tags.InvariantContains(dbTag))
                                {
                                    // Odd, this means there's more tags in the DB than in the text!?
                                    if (inconsistencies.TryGetValue(content.Id, out var propAliases))
                                        propAliases.Add(tagProp.Alias);
                                    else
                                        inconsistencies.Add(content.Id, new HashSet<string> { tagProp.Alias });
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
                    ActionParameters = new Dictionary<string, object>{["contentIds"] = inconsistencies}
                }
            };

            yield return new HealthCheckStatus($"There are {inconsistencies.Count} documents that have inconsistent tag data")
            {
                ResultType = inconsistencies.Count == 0 ? StatusResultType.Success : StatusResultType.Error,
                Actions = inconsistencies.Count == 0 ? Enumerable.Empty<HealthCheckAction>() : actions,
                
            };
        }
    }
}
