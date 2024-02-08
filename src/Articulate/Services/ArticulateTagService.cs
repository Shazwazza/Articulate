using System.Collections.Generic;
using Articulate.Models;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Web.Common;

namespace Articulate.Services
{
    public class ArticulateTagService : RepositoryService
    {
        private readonly IArticulateTagRepository _repository;

        public ArticulateTagService(
            IArticulateTagRepository repository,
            IScopeProvider provider,
            ILoggerFactory loggerFactory,
            IEventMessagesFactory eventMessagesFactory)
            : base(provider, loggerFactory, eventMessagesFactory)
        {
            _repository = repository;
        }

        // TODO: Wrap the repo

        public IEnumerable<PostsByTagModel> GetContentByTags(
            UmbracoHelper helper,
            ITagQuery tagQuery,
            IMasterModel masterModel,
            string tagGroup,
            string baseUrlName)
        {
            using (ScopeProvider.CreateCoreScope(autoComplete: true))
            {
                return _repository.GetContentByTags(
                    helper,
                    tagQuery,
                    masterModel,
                    tagGroup,
                    baseUrlName);
            }
        }

        public PostsByTagModel GetContentByTag(
            UmbracoHelper helper,
            IMasterModel masterModel,
            string tag,
            string tagGroup,
            string baseUrlName,
            long page,
            long pageSize)
        {
            using (ScopeProvider.CreateCoreScope(autoComplete: true))
            {
                return _repository.GetContentByTag(
                    helper,
                    masterModel,
                    tag,
                    tagGroup,
                    baseUrlName,
                    page,
                    pageSize);
            }
        }

        public IEnumerable<string> GetAllCategories(
            IMasterModel masterModel)
        {
            using (ScopeProvider.CreateCoreScope(autoComplete: true))
            {
                return _repository.GetAllCategories(masterModel);
            }
        }
    }
}
