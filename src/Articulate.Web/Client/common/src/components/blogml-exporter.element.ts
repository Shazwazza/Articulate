import { customElement, html, property, query, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUIButtonState } from '@umbraco-cms/backoffice/external/uui';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { type UmbModalManagerContext, UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { tryExecute, type UmbProblemDetails } from '@umbraco-cms/backoffice/resources';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { articulateDocumentTypeKey, documentById, openNodePicker } from '../utils/document-node-utils.js';
import { renderErrorMessage, renderHeaderActions, toUmbProblemDetails } from '../utils/template-utils.js';
import { downloadBlob, getDownloadFileName } from '../utils/download.js';
import { BoxStyles, ErrorBoxStyles, FormStyles, HostStyles, NodePickerStyles } from '../utils/style-utils.js';
import { BlogMlService } from '@api/sdk.gen.js';
import type { ExportModel } from '@api/types.gen.js';

/**
 * A LitElement-based component for exporting blog content in BlogML format.
 * Provides a form to select a blog node and export its content.
 *
 * @element blogml-exporter
 * @extends UmbLitElement
 */
@customElement('blogml-exporter')
export default class BlogMlExporterElement extends UmbLitElement {
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
  @state() declare _formState: UUIButtonState;
  /**
   * Holds an error object if a form operation fails.
   * @type {UmbProblemDetails | null}
   */
  @state() declare _formError: UmbProblemDetails | null;
  /**
   * The UDI of the selected Articulate blog node for export.
   * @private
   * @type {string | undefined}
   */
  @state() declare private _articulateBlogNode: string | undefined;
  /**
   * The name of the selected blog node, displayed in the input.
   * @private
   * @type {string}
   */
  @state() declare private _selectedBlogNodeName: string;

  /**
   * The main form element.
   * @private
   * @type {HTMLFormElement}
   */
  @query('#blogMlExportForm')
  private _form!: HTMLFormElement;

  /**
   * The modal manager context, used for opening the node picker.
   * @private
   * @type {UmbModalManagerContext | undefined}
   */
  private _modalManagerContext?: UmbModalManagerContext;
  /**
   * The UDI of the Articulate Archive document type.
   * @private
   * @type {string | undefined}
   */
  private _archiveDoctypeUdi: string | undefined = undefined;

  #setError(error: unknown, title: string) {
    this._formState = 'failed';
    this._formError = toUmbProblemDetails(error, title);
  }

  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (instance) => {
      this._modalManagerContext = instance;
    });
  }

  /**
   * Fetches the Articulate Archive doctype UDI when the component connects.
   * @async
   */
  async connectedCallback() {
    super.connectedCallback();
    this._archiveDoctypeUdi = await articulateDocumentTypeKey(this);
    if (!this._archiveDoctypeUdi) {
      const error = new Error(
        'Could not find the Articulate Archive document type. Please ensure Articulate is installed correctly.',
      );
      error.name = 'Configuration Error';
      this.#setError(error, error.name);
    }
  }

  /**
   * Resets the component's state.
   * @param {boolean} [fullReset=false] If true, performs a full reset of the form and its state.
   */
  resetState(fullReset = false) {
    if (fullReset) {
      this._form?.reset();
      this._formState = undefined;
      this._formError = null;
      this._articulateBlogNode = undefined;
      this._selectedBlogNodeName = '';
    }
  }

  /**
   * Opens the Umbraco node picker to select an Articulate blog node.
   * @private
   * @async
   */
  private async _openNodePicker() {
    if (!this._archiveDoctypeUdi) return;

    this._formError = null;
    const udi = await openNodePicker(this._modalManagerContext!, this._archiveDoctypeUdi, this);
    if (udi) {
      const variant = await documentById(this, udi);
      if (!variant) {
        this.#setError(new Error(`Could not find a node with UDI: ${udi}`), 'Node Not Found');
        return;
      }
      this._articulateBlogNode = udi;
      this._selectedBlogNodeName = variant.name;
    }
  }

  /**
   * Type guard to check if a value is a Blob.
   * @param {unknown} value The value to check.
   * @returns {boolean} True if the value is a Blob.
   */
  #isBlob = (value: unknown): value is Blob => {
    return value instanceof Blob;
  };

  /**
   * Handles the form submission event.
   * @param {SubmitEvent} e The submit event.
   * @private
   * @async
   */
  #handleSubmit = async (e: SubmitEvent) => {
    e.preventDefault();

    if (!this._form) return;

    if (!this._form.checkValidity()) return;

    // The node picker value is held outside the native form control.
    if (!this._articulateBlogNode) {
      const validationError = new Error('A blog node must be selected before exporting.');
      validationError.name = 'Validation Error';
      this.#setError(validationError, validationError.name);
      return;
    }

    if (this._formState === 'waiting') return;

    this._formState = 'waiting';
    this._formError = null;

    try {
      await this.#performExport();
      this._formState = 'success';
      const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      notificationContext?.peek('positive', { data: { message: 'BlogML exported successfully!' } });
      this.resetState(true);
    } catch (error) {
      this.#setError(error, 'Export Failed');
    }
  };

  /**
   * Performs the BlogML export by calling the backend API.
   * @private
   * @async
   */
  #performExport = async () => {
    const formData = new FormData(this._form);
    const embedImages = formData.get('embedImages') === 'on';
    const payload: ExportModel = {
      articulateBlogNode: this._articulateBlogNode!,
      exportImagesAsBase64: embedImages,
    };
    const result = await tryExecute(this, BlogMlService.postBlogmlExport({ body: payload }), {
      disableNotifications: true,
    });
    if (result.error) {
      throw result.error;
    }
    if (!result.data) {
      throw new Error('The server returned an invalid response during export.');
    }
    const blob = result.data;
    if (!this.#isBlob(blob)) {
      throw new Error('The server did not return a file. Please check the server logs.');
    }
    const fileName = getDownloadFileName(result.response?.headers.get('content-disposition'), 'blog-export.xml');
    downloadBlob(blob, fileName);
  };

  /**
   * Handles the reset button click event.
   * @param {Event} e The click event.
   * @private
   */
  private _handleReset = (e: Event) => {
    e.preventDefault();
    this.resetState(true);
  };

  private get _submitButtonColor(): 'positive' | 'default' {
    return this._articulateBlogNode ? 'positive' : 'default';
  }

  override render() {
    return html`
      <uui-box headline="BlogML Exporter" headlinevariant="h2">
        ${renderHeaderActions(this.routerPath)}
        <uui-form>
          <form
            id="blogMlExportForm"
            @submit=${this.#handleSubmit}
            @input=${() => {
              this._formError = null;
              this._formState = undefined;
            }}>
            <umb-form-validation-message>
              <uui-form-layout-item>
                <div class="node-picker-container">
                  <uui-label for="articulateBlogNode" slot="label" required>Articulate blog node</uui-label>
                  <uui-input
                    id="articulateBlogNode"
                    name="articulateBlogNode"
                    placeholder="No node selected"
                    .value=${this._selectedBlogNodeName}
                    readonly
                    required
                    required-message="You must select a blog node"
                    class="node-picker-input"></uui-input>
                  <uui-button
                    look="outline"
                    label=${this._articulateBlogNode ? 'Change' : 'Choose'}
                    @click=${this._openNodePicker}></uui-button>
                </div>
                <div slot="description">Choose the Articulate blog node to export from</div>
              </uui-form-layout-item>
              <uui-form-layout-item>
                <uui-label slot="label" for="embedImages">Embed images?</uui-label>
                <uui-toggle id="embedImages" name="embedImages"></uui-toggle>
                <div slot="description">
                  Check if you want to embed images as base64 data in the output file. Useful if your site isn't going
                  to be HTTP accessible to the site you will be importing on.
                </div>
              </uui-form-layout-item>
            </umb-form-validation-message>

            <div class="form-actions">
              <uui-button
                type="submit"
                look="primary"
                .color=${this._submitButtonColor}
                .state=${this._formState}
                label="Submit"></uui-button>
              <uui-button type="reset" look="secondary" label="Reset" @click=${this._handleReset}></uui-button>
            </div>
          </form>
        </uui-form>
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
    NodePickerStyles,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'blogml-exporter': BlogMlExporterElement;
  }
}
