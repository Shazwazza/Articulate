import { type TemplateResult, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbApiError, type UmbProblemDetails } from '@umbraco-cms/backoffice/resources';

/**
 * Renders a header action button that navigates back to the Articulate dashboard.
 * @param {string} [routerPath] Optional router path for the back button. Defaults to the main Articulate dashboard.
 * @returns {TemplateResult} The Lit `TemplateResult` for the header actions slot.
 */
export function renderHeaderActions(routerPath?: string): TemplateResult {
  return html`
    <div slot="header-actions">
      <uui-button
        label="Back to Articulate dashboard options"
        look="outline"
        compact
        href=${routerPath || '/umbraco/section/settings/dashboard/articulate'}>
        ← Back
      </uui-button>
    </div>
  `;
}

/**
 * Renders an error message box template if errors are present.
 * @param {UmbProblemDetails | null} error An Umbraco ProblemDetails response, or null if there are no errors.
 * @returns {TemplateResult | typeof nothing} The Lit `TemplateResult` for the error message, or `nothing` if no errors are provided.
 */
/**
 * Cleans up a model property path for display in the UI.
 * e.g., '$.newThemeName' -> 'New Theme Name'
 * @param {string} fieldName The raw field name from a validation errors dictionary.
 * @returns {string} A cleaned, human-readable field name.
 */
function cleanFieldName(fieldName: string): string {
  const cleaned = fieldName.startsWith('$.') ? fieldName.substring(2) : fieldName;
  return cleaned.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (str) => str.toUpperCase());
}

export function renderErrorMessage(error: UmbProblemDetails | null): TemplateResult | typeof nothing {
  if (!error) {
    return nothing;
  }

  const details = [
    ...(error.detail ? [error.detail] : []),
    ...(error.errors
      ? Object.entries(error.errors).flatMap(([field, messages]) =>
          messages.map((message) => `${cleanFieldName(field)}: ${message}`),
        )
      : []),
  ];

  return html`
    <div class="articulate-error-box">
      <strong>${error.title}</strong>
      ${
        details.length > 0
          ? html`
              <ul class="articulate-error-list">
                ${details.map((e) => html` <li>${e}</li> `)}
              </ul>
            `
          : nothing
      }
    </div>
  `;
}

/**
 * Converts an error into the native ProblemDetails shape used by Umbraco's
 * backoffice error handling. This only supplies a local fallback for errors
 * raised before an API response exists.
 */
export function toUmbProblemDetails(error: unknown, fallbackTitle: string): UmbProblemDetails {
  console.warn('[toUmbProblemDetails] Received error:', error);

  if (UmbApiError.isUmbApiError(error)) {
    return error.problemDetails;
  }

  if (typeof error === 'object' && error !== null && 'title' in error && 'status' in error) {
    return error as UmbProblemDetails;
  }

  const detail = error instanceof Error ? error.message : typeof error === 'string' ? error : undefined;

  return {
    type: 'about:blank',
    title: fallbackTitle,
    status: 0,
    ...(detail ? { detail } : {}),
  };
}
