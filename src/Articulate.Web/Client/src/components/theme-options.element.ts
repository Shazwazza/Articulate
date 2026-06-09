import { css, customElement, html, property, query, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUIButtonState } from '@umbraco-cms/backoffice/external/uui';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';

import { UmbValidationContext } from '@umbraco-cms/backoffice/validation';
import { ThemeOptionsService } from '../api/sdk.gen.js';
import { type IFormController, setFormError } from '../utils/form-utils.js';
import { showUmbracoNotification } from '../utils/notification-utils.js';
import { renderErrorMessage, renderHeaderActions } from '../utils/template-utils.js';
import { BoxStyles, ErrorBoxStyles, FormStyles, HostStyles } from '../utils/style-utils.js';

/**
 * A LitElement-based component for theme options e.g. copying an existing theme.
 *
 * @element theme-options
 * @extends UmbLitElement
 * @implements {IFormController}
 */
@customElement('theme-options')
export default class ThemeOptionsElement extends UmbLitElement implements IFormController {
  /**
   * Optional router path for the back button.
   * @type {string | undefined}
   */
  @property({ type: String })
  routerPath?: string;

  /**
   * The current state of the form button.
   * @type {UUIButtonState}
   */
  @state() _formState: UUIButtonState = undefined;
  /**
   * Holds an error object if a form operation fails.
   * @type {{ title: string; details: string[] } | null}
   */
  @state() _formError: { title: string; details: string[] } | null = null;
  /**
   * A list of available themes.
   * @private
   * @type {string[]}
   */
  @state() private _themes: string[] = [];
  /**
   * The name of the theme currently selected for duplication.
   * @private
   * @type {string | undefined}
   */
  @state() private _selectedTheme: string | undefined = undefined;
  /**
   * The name for the copied theme.
   * @private
   * @type {string | undefined}
   */
  @state() private _themeName: string | undefined = undefined;

  /**
   * The form element for duplicating a theme.
   * @private
   * @type {HTMLFormElement}
   */
  @query('form') private _form!: HTMLFormElement;

  #validation = new UmbValidationContext(this);

  /**
   * Loads the list of themes when the component is connected to the DOM.
   * @async
   */
  async connectedCallback() {
    super.connectedCallback();
    await this.#loadThemes();
  }

  /**
   * Resets the component's state.
   * @param {boolean} [fullReset=false] If true, performs a full reset, clearing the selected theme and form state.
   */
  resetState(fullReset = false) {
    if (fullReset) {
      this._formState = undefined;
      this._formError = null;
      this._selectedTheme = undefined;
      this._themeName = undefined;
    }
  }

  /**
   * Fetches the list of available themes from the server.
   * @private
   * @async
   */
  async #loadThemes() {
    try {
      const result = await ThemeOptionsService.getThemeDefault();
      if (!result.response.ok || !result.data) {
        throw result.error || new Error('The list of themes could not be retrieved from the server.');
      }
      this._themes = result.data?.map((theme) => theme) ?? [];
    } catch (error) {
      setFormError(this, error, 'Could not load themes');
    }
  }

  /**
   * Sets the selected theme and pre-fills the theme name.
   * @param {string} theme The name of the theme to select.
   * @private
   */
  #selectTheme(theme: string) {
    this.resetState(true);
    this._selectedTheme = theme;
    this._themeName = `Custom${theme}Theme`;
  }

  /**
   * Handles the click event for the select button on a theme card.
   * @param {Event} event The click event.
   * @param {string} themeName The name of the theme to select.
   * @private
   */
  #handleSelectThemeButtonClick(event: Event, themeName: string) {
    event.stopPropagation();
    this.#selectTheme(themeName);
  }

  /**
   * Handles the selection of a theme card.
   * @param {Event} event The selection event.
   * @private
   */
  #onCardSelected(event: Event) {
    const card = event.target as HTMLElement;
    const theme = card.getAttribute('data-theme');
    if (theme) {
      this.#selectTheme(theme);
    }
  }

  /**
   * Handles the deselection of a theme card.
   * @param {Event} event The deselection event.
   * @private
   */
  #onCardDeselected(event: Event) {
    const card = event.target as HTMLElement;
    const theme = card.getAttribute('data-theme');
    if (theme && theme === this._selectedTheme) {
      this.resetState(true);
    }
  }

  /**
   * Handles changes to the theme name input field.
   * @param {Event} e The input event.
   * @private
   */
  #onThemeNameChange = (e: Event) => {
    this._formError = null;
    this._formState = undefined;
    this._themeName = (e.target as HTMLInputElement).value;
  };

  /**
   * Handles the form submission for duplicating a theme.
   * @param {Event} e The submit event.
   * @private
   * @async
   */
  async #handleSubmit(e: Event) {
    e.preventDefault();
    if (!this._form) return;

    try {
      await this.#validation.validate();
    } catch (error) {
      setFormError(this, error, 'Validation Failed');
      return;
    }

    // validate() does not appear to work with other some uui elements, so backup validation, likely un-needed for a input box...
    if (!this._selectedTheme || !this._themeName) {
      const validationError = new Error('Please select a theme to copy and provide the theme name.');
      validationError.name = 'Validation Error';
      setFormError(this, validationError, validationError.name);
      return;
    }

    if (this._formState === 'waiting') return;

    this._formState = 'waiting';
    this._formError = null;

    try {
      const result = await ThemeOptionsService.postThemeCopy({
        body: {
          themeName: this._selectedTheme!,
          newThemeName: this._themeName!,
        },
      });
      if (!result.response.ok) {
        throw result.error || new Error('Failed to copy theme.');
      }

      const newThemeName = result.data ?? this._themeName!;
      this._formState = 'success';
      await showUmbracoNotification(
        this,
        `"${newThemeName}" created; review theme README.md for next steps.`,
        'positive',
        true,
      );
      this.resetState(true);
    } catch (error) {
      setFormError(this, error, 'Copy Failed');
    }
  }

  /**
   * Handles the reset/cancel button click event.
   * @param {Event} e The click event.
   * @private
   */
  #handleReset = (e: Event) => {
    e.preventDefault();
    this.resetState(true);
  };

  private get _submitButtonColor(): 'positive' | 'primary' {
    return this._selectedTheme && this._themeName ? 'positive' : 'primary';
  }

  /**
   * Renders the grid of available themes.
   * @returns {TemplateResult} The rendered HTML template.
   * @private
   */
  #renderThemeGrid() {
    return html`
      <div class="theme-grid">
        ${(this._themes ?? []).map(
          (theme: string) => html`
            <uui-card-media
              class="theme-card"
              .name=${theme}
              ?selectable=${this._formState !== 'waiting'}
              ?selected=${this._selectedTheme === theme}
              selectOnly
              @selected=${this.#onCardSelected}
              @deselected=${this.#onCardDeselected}
              data-theme=${theme}
              role="radio"
              aria-checked=${this._selectedTheme === theme}
              aria-label=${`Select theme ${theme}`}
              tabindex="0">
              <img
                class="theme-preview-img"
                src="/App_Plugins/Articulate/BackOffice/assets/theme-${theme.toLowerCase()}.png"
                alt="${theme} theme preview"
                loading="lazy"
                @error=${(e: Event) => {
                  const img = e.target as HTMLImageElement;
                  img.style.display = 'none';

                  const parent = img.parentElement;
                  if (!parent) return;

                  if (!parent.querySelector(':scope > .theme-fallback-initial')) {
                    const span = document.createElement('span');
                    span.className = 'theme-fallback-initial';
                    span.textContent = theme.charAt(0).toUpperCase();
                    parent.appendChild(span);
                  }
                }} />
              <div slot="actions">
                <uui-button
                  look="primary"
                  label="Select Theme ${theme}"
                  @click=${(e: Event) => this.#handleSelectThemeButtonClick(e, theme)}>
                  Select
                </uui-button>
              </div>
            </uui-card-media>
          `,
        )}
      </div>
    `;
  }

  /**
   * Renders the form for entering the theme name.
   * @returns {TemplateResult} The rendered HTML template.
   * @private
   */
  #renderDuplicateForm() {
    if (!this._selectedTheme) {
      return html``;
    }

    return html`
      <div class="duplicate-form">
        <h3>Copy '${this._selectedTheme}' Theme</h3>
        <p>Create a copy of this theme that you can customize.</p>
        <uui-form>
          <form
            @submit=${this.#handleSubmit}
            @input=${() => {
              this._formError = null;
              this._formState = undefined;
            }}>
            <uui-form-validation-message>
              <uui-form-layout-item>
                <uui-label for="themeName" slot="label" required>Theme name</uui-label>
                <uui-input
                  id="themeName"
                  name="themeName"
                  .value=${this._themeName ?? ''}
                  @input=${this.#onThemeNameChange}
                  required
                  required-message="You must provide a name for the theme."
                  label="Theme name"></uui-input>
              </uui-form-layout-item>
            </uui-form-validation-message>
            <div class="form-actions">
              <uui-button
                id="duplicateButton"
                type="submit"
                look="primary"
                .color=${this._submitButtonColor}
                .state=${this._formState}>
                Create Theme
              </uui-button>
              <uui-button id="cancelButton" type="reset" look="secondary" @click=${this.#handleReset}>
                Cancel
              </uui-button>
            </div>
          </form>
        </uui-form>
      </div>
    `;
  }

  override render() {
    return html`
      <uui-box headline="Theme Options">
        ${renderHeaderActions(this.routerPath)}
        <div class="container">
          <p>
            Articulate's built-in themes are shown here for reference. You can copy them, but the new theme name must
            not match a built-in theme name.
          </p>
        </div>
        <div class="container">${this.#renderThemeGrid()} ${this.#renderDuplicateForm()}</div>
        ${this._formError ? renderErrorMessage(this._formError) : ''}
      </uui-box>
    `;
  }

  /**
   * The styles for the component.
   * @static
   * @readonly
   */
  static override readonly styles = [
    UmbTextStyles,
    HostStyles,
    BoxStyles,
    ErrorBoxStyles,
    FormStyles,
    css`
      .theme-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
        gap: var(--uui-size-space-6);
        margin-bottom: var(--uui-size-space-6);
      }
      .theme-card {
        cursor: pointer;
        border: 1px solid var(--uui-color-border-emphasis);
        width: 100%;
        height: 200px;
        aspect-ratio: 1;
        box-sizing: border-box;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: space-between;
        padding: var(--uui-size-space-2);
      }
      .theme-card:hover {
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
        transform: translateY(-2px);
        transition: all 0.2s ease;
      }
      .theme-preview-img {
        border-bottom: 1px solid var(--uui-color-border);
        object-fit: contain;
        background-color: var(--uui-color-surface-alt);
        border-radius: var(--uui-border-radius);
        box-sizing: border-box;
      }
      .theme-fallback-initial {
        display: flex;
        align-items: center;
        justify-content: center;
        width: 100%;
        height: 100%;
        font-size: 3rem;
        font-weight: bold;
        color: var(--uui-color-text-alt);
        background-color: var(--uui-color-surface-alt);
        border-radius: var(--uui-border-radius);
        box-sizing: border-box;
      }
      .duplicate-form {
        background: var(--uui-color-surface);
        padding: var(--uui-size-space-4);
        border-radius: var(--uui-border-radius);
        margin-top: var(--uui-size-space-6);
        border-top: 1px solid var(--uui-color-divider);
        padding: var(--uui-size-space-3);
      }

      .duplicate-form h3 {
        margin-top: 0;
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'theme-options': ThemeOptionsElement;
  }
}
