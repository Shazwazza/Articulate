import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { css, customElement, html, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import {
  UmbPropertyValueChangeEvent,
  type UmbPropertyEditorConfigCollection,
  type UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';

import { ThemePickerService } from '../api/sdk.gen.js';
import { formatApiError } from '../utils/error-utils.js';

/**
 * A custom element for picking an Articulate theme.
 * @element theme-picker-element
 * @extends UmbElementMixin(UmbLitElement)
 * @implements {UmbPropertyEditorUiElement}
 */
@customElement('theme-picker-element')
export default class ThemePickerElement extends UmbElementMixin(UmbLitElement) implements UmbPropertyEditorUiElement {
  /**
   * The selected theme name, which is the value saved for the property.
   * @type {string | undefined}
   */
  @property()
  value?: string;

  /**
   * The configuration for the property editor.
   * @type {UmbPropertyEditorConfigCollection | undefined}
   */
  @property({ attribute: false })
  config?: UmbPropertyEditorConfigCollection;

  /**
   * An array of theme options for the select input.
   * @private
   * @type {Array<{ name: string; value: string; selected?: boolean }>}
   */
  @state()
  private _themeSelectOptions: Array<{ name: string; value: string; selected?: boolean }> = [];

  /**
   * Holds an error object if fetching themes fails.
   * @private
   * @type {{ title: string; details: string[] } | null}
   */
  @state()
  private _error: { title: string; details: string[] } | null = null;

  constructor() {
    super();
  }

  async connectedCallback() {
    super.connectedCallback();
    this._fetchThemes();
  }

  override updated(changedProperties: Map<PropertyKey, unknown>) {
    super.updated(changedProperties);

    if (changedProperties.has('value') && this._themeSelectOptions.length > 0) {
      this._themeSelectOptions = this._themeSelectOptions.map((option) => ({
        ...option,
        selected: !!this.value && option.value === this.value,
      }));
    }
  }

  /**
   * Fetches the available themes from the server and populates the select options.
   * @private
   * @async
   */
  private async _fetchThemes() {
    this._error = null;

    const result = await ThemePickerService.getEditorsThemePickerThemes();

    if (!result.response.ok || !result.data) {
      this._error = formatApiError(result.error, 'Failed to load themes from the server.');
      return;
    }

    const data = result.data;

    this._themeSelectOptions = data.map((theme) => ({
      name: theme,
      value: theme,
      selected: !!this.value && theme === this.value,
    }));
  }

  /**
   * Handles the change event from the select input, updates the value, and dispatches a change event.
   * @param {Event} event The input change event.
   * @private
   */
  private _handleInput(event: Event) {
    const newValue = (event.target as HTMLSelectElement).value as string | undefined;

    if (this.value !== newValue) {
      this.value = newValue;
      this.dispatchEvent(new UmbPropertyValueChangeEvent());
    }
  }

  override render() {
    if (this._error) {
      return html` <span style="color: var(--uui-color-danger);">${this._error.title}</span> `;
    }

    return html`
      <uui-select
        .options=${this._themeSelectOptions}
        .value=${this.value}
        @change=${this._handleInput}
        label="Select a theme"></uui-select>
    `;
  }

  /**
   * The styles for the component.
   * @static
   * @readonly
   */
  static override readonly styles = [
    UmbTextStyles,
    css`
      uui-select {
        width: 100%;
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'theme-picker-element': ThemePickerElement;
  }
}
