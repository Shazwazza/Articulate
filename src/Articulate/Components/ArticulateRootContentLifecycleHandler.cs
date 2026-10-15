#nullable enable
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Articulate.Components
{
    /// <summary>
    /// Ensures required Articulate root child nodes exist on save and are published with the root when needed.
    /// </summary>
    internal class ArticulateRootContentLifecycleHandler(
        IContentTypeService contentTypeService,
        IContentService contentService,
        ILanguageService languageService,
        ILogger<ArticulateRootContentLifecycleHandler> logger,
        IScopeProvider scopeProvider)
        : INotificationAsyncHandler<ContentSavedNotification>,
            INotificationAsyncHandler<ContentPublishedNotification>
    {
        /// <inheritdoc/>
        public async Task HandleAsync(ContentSavedNotification notification, CancellationToken cancellationToken)
        {
            foreach (IContent c in notification.SavedEntities)
            {
                if (!c.WasPropertyDirty("Id") ||
                    !c.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.Articulate))
                {
                    continue;
                }

                var defaultLang = await languageService.GetDefaultIsoCodeAsync();
                cancellationToken.ThrowIfCancellationRequested();

                using IScope scope = scopeProvider.CreateScope(autoComplete: true);
                _ = EnsureChildNodeExists(
                    c,
                    ArticulateConstants.ContentType.ArticulateArchive,
                    ArticulateConstants.Convention.ArticlesDocument,
                    defaultLang,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                _ = EnsureChildNodeExists(
                    c,
                    ArticulateConstants.ContentType.ArticulateAuthors,
                    ArticulateConstants.Convention.AuthorsDocument,
                    defaultLang,
                    cancellationToken);
            }
        }

        /// <inheritdoc/>
        public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
        {
            foreach (IContent root in notification.PublishedEntities)
            {
                if (!root.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.Articulate))
                {
                    continue;
                }

                var defaultLang = await languageService.GetDefaultIsoCodeAsync();
                cancellationToken.ThrowIfCancellationRequested();

                using IScope scope = scopeProvider.CreateScope(autoComplete: true);
                PublishRequiredChildNode(
                    EnsureChildNodeExists(
                        root,
                        ArticulateConstants.ContentType.ArticulateArchive,
                        ArticulateConstants.Convention.ArticlesDocument,
                        defaultLang,
                        cancellationToken),
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                PublishRequiredChildNode(
                    EnsureChildNodeExists(
                        root,
                        ArticulateConstants.ContentType.ArticulateAuthors,
                        ArticulateConstants.Convention.AuthorsDocument,
                        defaultLang,
                        cancellationToken),
                    cancellationToken);
            }
        }

        private IContent? EnsureChildNodeExists(
            IContent parent,
            string contentTypeAlias,
            string documentName,
            string defaultLang,
            CancellationToken cancellationToken)
        {
            IContentType? contentType = contentTypeService.Get(contentTypeAlias);
            if (contentType is null)
            {
                logger.LogWarning(
                    "Content type {ContentTypeAlias} not found when ensuring child for parent {ParentId}",
                    contentTypeAlias,
                    parent.Id);
                return null;
            }

            IContent? existingChild = GetChild(parent.Id, contentType.Id, cancellationToken);
            if (existingChild is not null)
            {
                return existingChild;
            }

            IContent child = contentService.Create(string.Empty, parent, contentTypeAlias);
            if (contentType.VariesByCulture())
            {
                child.SetCultureName(documentName, defaultLang);
            }
            else
            {
                child.Name = documentName;
            }

            OperationResult saveResult = contentService.Save(child);
            if (!saveResult.Success)
            {
                existingChild = GetChild(parent.Id, contentType.Id, cancellationToken);
                if (existingChild is not null)
                {
                    return existingChild;
                }

                logger.LogError(
                    "Failed to save {ContentType} node for parent {ParentId}: {SaveResult}",
                    contentTypeAlias,
                    parent.Id,
                    saveResult);
                return null;
            }

            return child;
        }

        private void PublishRequiredChildNode(IContent? child, CancellationToken cancellationToken)
        {
            if (child is null || child.Published)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            PublishResult publishResult = contentService.Publish(child, ["*"]);
            if (!publishResult.Success)
            {
                logger.LogError(
                    "Failed to publish required child node {NodeId} ({ContentType}): {PublishResult}",
                    child.Id,
                    child.ContentType.Alias,
                    publishResult);
            }
        }

        private IContent? GetChild(int parentId, int contentTypeId, CancellationToken cancellationToken)
        {
            const int pageSize = 50;
            var pageIndex = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    IEnumerable<IContent> page =
                        contentService.EnumeratePagedChildren(parentId, pageIndex, pageSize, out var total);
                    var items = page.ToList();

                    IContent? existingChild = items.FirstOrDefault(x => x.ContentTypeId == contentTypeId);
                    if (existingChild is not null)
                    {
                        return existingChild;
                    }

                    if (items.Count == 0 || (pageIndex + 1) * pageSize >= total)
                    {
                        return null;
                    }

                    pageIndex++;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Error checking if child exists for parent {ParentId} with type {ContentTypeId}",
                        parentId,
                        contentTypeId);
                    return null;
                }
            }
        }
    }
}
