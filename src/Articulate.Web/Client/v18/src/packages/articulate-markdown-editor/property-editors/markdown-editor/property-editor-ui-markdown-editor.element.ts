import type { ArticulateInputMarkdownElement } from '../../components/input-markdown-editor/index.js';
import { html, customElement, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type {
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';
import type { UUIModalSidebarSize } from '@umbraco-cms/backoffice/external/uui';

import '../../components/input-markdown-editor/index.js';
import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from '@umbraco-cms/backoffice/validation';

/**
 * @element articulate-property-editor-ui-markdown-editor
 */
@customElement('articulate-property-editor-ui-markdown-editor')
export class ArticulatePropertyEditorUIMarkdownEditorElement
  extends UmbFormControlMixin<string, typeof UmbLitElement>(UmbLitElement)
  implements UmbPropertyEditorUiElement
{
  /**
   * Sets the input to readonly mode, meaning value cannot be changed but still able to read and select its content.
   * @type {boolean}
   * @attr
   * @default false
   */
  @property({ type: Boolean, reflect: true })
  readonly = false;
  /**
   * Sets the input to mandatory, meaning validation will fail if the value is empty.
   * @type {boolean}
   */
  @property({ type: Boolean })
  mandatory?: boolean;
  @property({ type: String })
  mandatoryMessage = UMB_VALIDATION_EMPTY_LOCALIZATION_KEY;

  @state()
  private _preview?: boolean;

  @state()
  private _overlaySize: UUIModalSidebarSize = 'small';

  public set config(config: UmbPropertyEditorConfigCollection | undefined) {
    if (!config) return;

    this._preview = config.getValueByAlias('preview');
    this._overlaySize = config.getValueByAlias('overlaySize') ?? 'small';
  }

  #onChange(event: Event & { target: ArticulateInputMarkdownElement }) {
    this.value = event.target.value as string;
    this.dispatchEvent(new UmbChangeEvent());
  }

  protected override firstUpdated() {
    this.addFormControlElement(this.shadowRoot!.querySelector('articulate-input-markdown')!);
  }
  override render() {
    return html`
      <articulate-input-markdown
        .value=${this.value}
        .overlaySize=${this._overlaySize}
        ?preview=${this._preview}
        @change=${this.#onChange}
        ?readonly=${this.readonly}
        ?required=${this.mandatory}
        .requiredMessage=${this.mandatoryMessage}></articulate-input-markdown>
    `;
  }
}

export { ArticulatePropertyEditorUIMarkdownEditorElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'articulate-property-editor-ui-markdown-editor': ArticulatePropertyEditorUIMarkdownEditorElement;
  }
}
