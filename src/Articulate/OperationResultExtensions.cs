#nullable enable
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;

namespace Articulate
{
    internal static class OperationResultExtensions
    {
        public static void EnsureSuccess<TResultType>(
            this OperationResult<TResultType> result,
            ILogger logger,
            string operationDescription)
            where TResultType : struct
        {
            if (result.Success)
            {
                return;
            }

            var summary = BuildEventMessageSummary(result.EventMessages);
            ThrowLoggedOperationException(logger, operationDescription, result.Result, summary);
        }

        // Intentionally logs before throwing so current callers (MetaWeblog provider & BlogML importer)
        // get consistent operation context without duplicating logging at each call site. If future
        // callers log independently this can be revisited, but for now this centralizes the log entry.
        private static void ThrowLoggedOperationException(
            ILogger logger,
            string operationDescription,
            object? status,
            string summary)
        {
            var exception =
                new InvalidOperationException(
                    $"Failed to {operationDescription}. Status: {status}. Details: {summary}");
            logger.LogError(
                exception,
                "Failed to {OperationDescription}. Status: {Status}. Details: {Details}",
                operationDescription,
                status,
                summary);
            throw exception;
        }

        private static string BuildEventMessageSummary(EventMessages? eventMessages)
        {
            if (eventMessages is null || eventMessages.Count == 0)
            {
                return "No event messages were recorded.";
            }

            return string.Join("; ", eventMessages.GetAll().Select(m => $"{m.Category}: {m.Message}"));
        }
    }
}
