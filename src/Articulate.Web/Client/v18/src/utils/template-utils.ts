import { type TemplateResult, html, nothing } from '@umbraco-cms/backoffice/external/lit';

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
 * @param {{ title: string; details: string[] } | null} errors An object containing the error title and details, or null if there are no errors.
 * @returns {TemplateResult | typeof nothing} The Lit `TemplateResult` for the error message, or `nothing` if no errors are provided.
 */
export function renderErrorMessage(
  errors: { title: string; details: string[] } | null,
): TemplateResult | typeof nothing {
  if (!errors) {
    console.info('At validation event: renderErrorMessage returning nothing as errors object is null');
    return nothing;
  }

  const { title, details } = errors;

  return html`
    <div class="articulate-error-box">
      <strong>${title}</strong>
      ${details.length > 0
        ? html`
            <ul class="articulate-error-list">
              ${details.map((e) => html` <li>${e}</li> `)}
            </ul>
          `
        : nothing}
    </div>
  `;
}
