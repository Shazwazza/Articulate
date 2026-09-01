import { css, customElement, html, property, query, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUIButtonState, UUIInputFileElement } from '@umbraco-cms/backoffice/external/uui';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { type UmbModalManagerContext, UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { tryExecute, type UmbProblemDetails } from '@umbraco-cms/backoffice/resources';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { keyed } from 'lit-html/directives/keyed.js';
import { articulateDocumentTypeKey, documentById, openNodePicker } from '../utils/document-node-utils.js';
import { renderErrorMessage, renderHeaderActions, toUmbProblemDetails } from '../utils/template-utils.js';
import { downloadBlob, getDownloadFileName } from '../utils/download.js';
import { BoxStyles, ErrorBoxStyles, FormStyles, HostStyles, NodePickerStyles } from '../utils/style-utils.js';
import type { ImportFileResponse, ImportModel, ImportResponse } from '@api/types.gen.js';
import { BlogMlService } from '@api/sdk.gen.js';

/**
 * A LitElement-based component for importing blog content from a BlogML file.
 *
 * @element blogml-importer
 * @extends UmbLitElement
 */
@customElement('blogml-importer')
export default class BlogMlImporterElement extends UmbLitElement {
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
   * The UDI of the selected Articulate blog node for import.
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
   * The number of posts found in the uploaded BlogML file.
   * @private
   * @type {number | undefined}
   */
  @state() declare private _postCount: number | undefined;
  /**
   * The number of external image attachments found in the uploaded BlogML file.
   * @private
   * @type {number}
   */
  @state() declare private _externalImageCount: number;
  /**
   * The unique external hosts referenced by the uploaded BlogML file.
   * @private
   * @type {string[]}
   */
  @state() declare private _externalHosts: string[];
  /**
   * The external hosts referenced by the uploaded BlogML file that are not currently allowlisted.
   * @private
   * @type {string[]}
   */
  @state() declare private _blockedExternalHosts: string[];
  /**
   * The temporary file name returned by preflight for the currently selected BlogML file.
   * @private
   * @type {string | undefined}
   */
  @state() declare private _tempFileName: string | undefined;
  /**
   * Whether the "import first image" option is currently enabled in the form.
   * @private
   * @type {boolean}
   */
  @state() declare private _importFirstImage: boolean;
  /**
   * Whether the selected BlogML file is currently being analyzed.
   * @private
   * @type {boolean}
   */
  @state() declare private _isPreflighting: boolean;
  /**
   * Monotonically increasing token used to ignore stale async file-analysis results.
   * @private
   * @type {number}
   */
  @state() declare private _analysisRequestId: number;
  /**
   * A key to force re-rendering of the form, used for resetting the file input.
   * @private
   * @type {number}
   */
  @state() declare private _formRenderKey: number;

  /**
   * The main form element.
   * @private
   * @type {HTMLFormElement}
   */
  @query('#blogMlImportForm')
  private _form!: HTMLFormElement;

  /**
   * The UUI file control that owns the selected File/FormData value.
   * @private
   */
  @query('#importFile')
  private _importFileInput!: UUIInputFileElement;

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
    this._externalImageCount = 0;
    this._externalHosts = [];
    this._blockedExternalHosts = [];
    this._importFirstImage = false;
    this._isPreflighting = false;
    this._analysisRequestId = 0;
    this._formRenderKey = 0;
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
    this._postCount = undefined;
    this._externalImageCount = 0;
    this._externalHosts = [];
    this._blockedExternalHosts = [];
    this._tempFileName = undefined;
    this._isPreflighting = false;
    this._analysisRequestId++;

    if (fullReset) {
      this._formState = undefined;
      this._formError = null;
      this._articulateBlogNode = undefined;
      this._selectedBlogNodeName = '';
      this._importFirstImage = false;
      // UUIInputFileElement does not clear its internal file preview on form reset.
      // Recreate the form so the selected file and its preflight state clear together.
      this._formRenderKey++;
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
   * Type guard to check if a value can be treated as a numeric response field.
   * @param {unknown} value The value to check.
   * @returns {boolean} True if the value is a number or numeric string.
   */
  #isNumericResponseValue = (value: unknown): value is number | string => {
    return (
      typeof value === 'number' || (typeof value === 'string' && value.trim() !== '' && !Number.isNaN(Number(value)))
    );
  };

  /**
   * Normalizes a numeric response field to a number.
   * @param {number | string} value The value to normalize.
   * @returns {number} The normalized number.
   */
  #toNumber = (value: number | string): number => {
    return typeof value === 'number' ? value : Number(value);
  };

  /**
   * Type guard to check if the data is a valid ImportFileResponse.
   * @param {unknown} data The data to check.
   * @returns {boolean} True if the data is an ImportFileResponse.
   */
  #isImportFileResponse = (data: unknown): data is ImportFileResponse => {
    return (
      typeof data === 'object' &&
      data !== null &&
      'temporaryFileName' in data &&
      typeof (data as ImportFileResponse).temporaryFileName === 'string' &&
      'postCount' in data &&
      this.#isNumericResponseValue((data as ImportFileResponse).postCount) &&
      'externalImageCount' in data &&
      this.#isNumericResponseValue((data as ImportFileResponse).externalImageCount) &&
      'externalHosts' in data &&
      Array.isArray((data as ImportFileResponse).externalHosts) &&
      'blockedExternalHosts' in data &&
      Array.isArray((data as ImportFileResponse).blockedExternalHosts)
    );
  };

  /**
   * Type guard to check if the data is a valid ImportResponse.
   * @param {unknown} data The data to check.
   * @returns {boolean} True if the data is an ImportResponse.
   */
  #isImportResponse = (data: unknown): data is ImportResponse => {
    return (
      typeof data === 'object' &&
      data !== null &&
      'postCount' in data &&
      this.#isNumericResponseValue((data as ImportResponse).postCount) &&
      'authorCount' in data &&
      this.#isNumericResponseValue((data as ImportResponse).authorCount) &&
      'commentCount' in data &&
      this.#isNumericResponseValue((data as ImportResponse).commentCount) &&
      'completed' in data
    );
  };

  #deleteTempFile = async () => {
    if (!this._tempFileName) return;

    try {
      await tryExecute(
        this,
        BlogMlService.deleteBlogmlImportFile({ query: { tempFile: this._tempFileName }, throwOnError: true }),
        { disableNotifications: true },
      );
    } catch {
      // Best-effort cleanup for abandoned preflight uploads.
    } finally {
      this._tempFileName = undefined;
    }
  };

  #getSelectedImportFile = (): File | undefined => {
    const value = this._importFileInput?.value;
    const importFile =
      value instanceof File
        ? value
        : value instanceof FormData
          ? value.get('importFile')
          : this._form
            ? new FormData(this._form).get('importFile')
            : undefined;
    return importFile instanceof File && importFile.size > 0 ? importFile : undefined;
  };

  #handleImportFileChange = async () => {
    this._analysisRequestId++;
    this._formError = null;
    this._formState = undefined;
    this._postCount = undefined;
    this._externalImageCount = 0;
    this._externalHosts = [];
    this._blockedExternalHosts = [];
    this._isPreflighting = false;
    await this.#deleteTempFile();
  };

  #handleImportFirstImageChange = (e: Event) => {
    const toggle = e.target as HTMLInputElement | null;
    this._importFirstImage = toggle?.checked ?? false;
  };

  #verifySelectedFile = async () => {
    const importFile = this.#getSelectedImportFile();
    const requestId = ++this._analysisRequestId;

    this._formError = null;
    this._formState = undefined;
    this._postCount = undefined;
    this._externalImageCount = 0;
    this._externalHosts = [];
    this._blockedExternalHosts = [];

    await this.#deleteTempFile();

    if (!importFile || importFile.size <= 0) {
      const validationError = new Error('A BlogML file must be selected before verification.');
      validationError.name = 'Validation Error';
      this.#setError(validationError, validationError.name);
      return;
    }

    this._isPreflighting = true;

    try {
      const initData = await this.#beginImport(importFile);
      if (requestId !== this._analysisRequestId) {
        await tryExecute(
          this,
          BlogMlService.deleteBlogmlImportFile({ query: { tempFile: initData.temporaryFileName }, throwOnError: true }),
          { disableNotifications: true },
        );
        return;
      }

      this._postCount = this.#toNumber(initData.postCount);
      this._externalImageCount = this.#toNumber(initData.externalImageCount);
      this._externalHosts = initData.externalHosts;
      this._blockedExternalHosts = initData.blockedExternalHosts;
      this._tempFileName = initData.temporaryFileName;
    } catch (error) {
      if (requestId !== this._analysisRequestId) {
        return;
      }

      this.#setError(error, 'Import Analysis Failed');
    } finally {
      if (requestId === this._analysisRequestId) {
        this._isPreflighting = false;
      }
    }
  };

  #restoreTempFileForRetry = async (importFile: File) => {
    const requestId = ++this._analysisRequestId;
    this._isPreflighting = true;

    try {
      const initData = await this.#beginImport(importFile);
      if (requestId !== this._analysisRequestId) {
        await tryExecute(
          this,
          BlogMlService.deleteBlogmlImportFile({ query: { tempFile: initData.temporaryFileName }, throwOnError: true }),
          { disableNotifications: true },
        );
        return;
      }

      this._postCount = this.#toNumber(initData.postCount);
      this._externalImageCount = this.#toNumber(initData.externalImageCount);
      this._externalHosts = initData.externalHosts;
      this._blockedExternalHosts = initData.blockedExternalHosts;
      this._tempFileName = initData.temporaryFileName;
    } catch {
      if (requestId === this._analysisRequestId) {
        this._tempFileName = undefined;
      }
    } finally {
      if (requestId === this._analysisRequestId) {
        this._isPreflighting = false;
      }
    }
  };

  /**
   * Handles the form submission by orchestrating the multi-step import process.
   * @param {SubmitEvent} e The submit event.
   * @private
   * @async
   */
  #handleSubmit = async (e: SubmitEvent) => {
    e.preventDefault();
    if (!this._form) return;

    if (!this._form.checkValidity()) return;

    const formData = new FormData(this._form);
    const importFile = this.#getSelectedImportFile();

    // The file and node-picker values are partly held outside native form controls.
    const validationRules = [
      {
        isValid: !!this._articulateBlogNode,
        message: 'A blog node must be selected before importing.',
      },
      {
        isValid: importFile && importFile.size > 0,
        message: 'A BlogML file must be selected for import.',
      },
      {
        isValid: !this._isPreflighting,
        message: 'Please wait for the BlogML file analysis to finish.',
      },
      {
        isValid: !!this._tempFileName,
        message: 'The selected BlogML file must be verified before importing.',
      },
    ];

    const firstInvalidRule = validationRules.find((rule) => !rule.isValid);

    if (firstInvalidRule) {
      const validationError = new Error(firstInvalidRule.message);
      validationError.name = 'Validation Error';
      this.#setError(validationError, validationError.name);
      return;
    }

    if (this._formState === 'waiting') return;

    this._formState = 'waiting';
    this._formError = null;

    try {
      const importData = await this.#finalizeImport(formData, this._tempFileName!);
      const commentCount = this.#toNumber(importData.commentCount);
      const authorCount = this.#toNumber(importData.authorCount);
      const postCount = this.#toNumber(importData.postCount);

      if (formData.get('exportDisqusXml') === 'on' && commentCount > 0) {
        await this.#exportDisqusComments();
      }

      this._formState = 'success';
      const disqusMessage =
        formData.get('exportDisqusXml') === 'on' && commentCount > 0
          ? `${commentCount} comments exported.`
          : formData.get('exportDisqusXml') === 'on'
            ? 'No comments found to export.'
            : '';

      const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      notificationContext?.stay('positive', {
        data: {
          message: `BlogML imported successfully! ${authorCount} authors, ${postCount} posts imported. ${disqusMessage}`,
        },
      });
      this.resetState(true);
    } catch (error) {
      this._tempFileName = undefined;
      if (importFile && importFile.size > 0) {
        void this.#restoreTempFileForRetry(importFile);
      }
      this.#setError(error, 'Import Failed');
    }
  };

  /**
   * Begins the import process by uploading the BlogML file to the server.
   * @param {File} importFile The BlogML file to upload.
   * @returns {Promise<ImportFileResponse>} A promise that resolves with the initial import data, including the temporary file name and post count.
   * @private
   * @async
   */
  #beginImport = async (importFile: File): Promise<ImportFileResponse> => {
    const result = await tryExecute(this, BlogMlService.postBlogmlImportFile({ body: { importFile } }), {
      disableNotifications: true,
    });

    if (result.error || !this.#isImportFileResponse(result.data)) {
      throw result.error || new Error('The server returned an invalid response when uploading the file.');
    }

    // Now that the type is confirmed, we can safely validate the content.
    if (!result.data.temporaryFileName || this.#toNumber(result.data.postCount) <= 0) {
      throw new Error('The blog import file appears to be empty or invalid.');
    }

    return result.data;
  };

  /**
   * Finalizes the import process by sending the import options to the server.
   * @param {FormData} formData The form data containing import options.
   * @param {string} tempFile The temporary file name returned from the beginImport step.
   * @returns {Promise<ImportResponse>} A promise that resolves with the final import results.
   * @private
   * @async
   */
  #finalizeImport = async (formData: FormData, tempFile: string): Promise<ImportResponse> => {
    const payload: ImportModel = {
      articulateBlogNode: this._articulateBlogNode!,
      overwrite: formData.get('overwrite') === 'on',
      publish: formData.get('publish') === 'on',
      regexMatch: (formData.get('regexMatch') as string) || '',
      regexReplace: (formData.get('regexReplace') as string) || '',
      tempFile: tempFile,
      exportDisqusXml: formData.get('exportDisqusXml') === 'on',
      importFirstImage: formData.get('importFirstImage') === 'on',
    };
    const result = await tryExecute(this, BlogMlService.postBlogmlImport({ body: payload }), {
      disableNotifications: true,
    });

    if (result.error || !this.#isImportResponse(result.data)) {
      throw result.error || new Error('The server returned an invalid response when finalizing the import.');
    }

    // Now that the type is confirmed, we can safely validate the content.
    if (!result.data.completed) {
      throw new Error('The server indicated that the import failed to complete.');
    }

    return result.data;
  };

  /**
   * Exports the Disqus comments to an XML file.
   * @private
   * @async
   */
  #exportDisqusComments = async () => {
    const result = await tryExecute(this, BlogMlService.getBlogmlExportDisqus(), {
      disableNotifications: true,
    });
    if (result.error || !result.data) {
      throw result.error || new Error('Failed to export Disqus comments.');
    }
    const blob = result.data;
    if (!this.#isBlob(blob)) {
      throw new Error('Invalid file received for Disqus export.');
    }
    const fileName = getDownloadFileName(result.response?.headers.get('content-disposition'), 'disqus-comments.xml');
    downloadBlob(blob, fileName);
  };

  /**
   * Handles the reset button click event.
   * @param {Event} e The click event.
   * @private
   */
  private _handleReset = (e: Event) => {
    e.preventDefault();
    void this.#deleteTempFile().finally(() => this.resetState(true));
  };

  override render() {
    return html`
      <uui-box headline="BlogML Importer" headlinevariant="h2">
        ${renderHeaderActions(this.routerPath)}
        <uui-form>
          ${keyed(
            this._formRenderKey,
            html`
              <form
                id="blogMlImportForm"
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
                    <div slot="description">Choose the Articulate blog node to import into</div>
                  </uui-form-layout-item>
                  <uui-form-layout-item>
                    <uui-label slot="label" for="importFile" required>BlogML import file</uui-label>
                    <uui-input-file
                      id="importFile"
                      accept="text/xml"
                      @change=${this.#handleImportFileChange}
                      @input=${this.#handleImportFileChange}
                      required
                      required-message="You must select a BlogML file to import"
                      name="importFile"
                      tabindex="0"></uui-input-file>
                    <div slot="description">
                      Select the BlogML file, then choose <strong>Verify file</strong> to analyse the declared external
                      hosts before import. The actual import still validates every fetched host and any redirects.
                    </div>
                  </uui-form-layout-item>
                  <uui-form-layout-item>
                    <uui-label slot="label" for="overwrite">Overwrite imported posts?</uui-label>
                    <uui-toggle id="overwrite" name="overwrite"></uui-toggle>
                    <div slot="description">Check if you want to overwrite posts already imported</div>
                  </uui-form-layout-item>
                  <uui-form-layout-item>
                    <uui-label slot="label" for="publish">Publish all posts?</uui-label>
                    <uui-toggle id="publish" name="publish"></uui-toggle>
                    <div slot="description">Check if you want all imported posts to be published</div>
                  </uui-form-layout-item>
                  <uui-form-layout-item>
                    <uui-label for="regexMatch" slot="label">Regex match expression</uui-label>
                    <uui-input
                      id="regexMatch"
                      class="auto-width-input"
                      name="regexMatch"
                      auto-width
                      placeholder="Example to match: (@example.old)"></uui-input>
                    <div slot="description">
                      Regex statement used to match content in the blog post to be replaced by the match statement. See
                      the Articulate Wiki Importing page for more information.
                    </div>
                  </uui-form-layout-item>
                  <uui-form-layout-item>
                    <uui-label for="regexReplace" slot="label">Regex replacement statement</uui-label>
                    <uui-input
                      id="regexReplace"
                      class="auto-width-input"
                      name="regexReplace"
                      auto-width
                      placeholder="Example replacement: @example.new"></uui-input>
                    <div slot="description">Replacement statement used with the above match statement</div>
                  </uui-form-layout-item>
                  <uui-form-layout-item>
                    <uui-label slot="label" for="exportDisqusXml">Export Disqus Xml</uui-label>
                    <uui-toggle id="exportDisqusXml" name="exportDisqusXml"></uui-toggle>
                    <div slot="description">
                      If you would like Articulate to output an XML file that you can use to import the comments found
                      in this file in to Disqus
                    </div>
                  </uui-form-layout-item>
                  <uui-form-layout-item>
                    <uui-label slot="label" for="importFirstImage">Import First Image from Post Attachments</uui-label>
                    <uui-toggle
                      id="importFirstImage"
                      name="importFirstImage"
                      @change=${this.#handleImportFirstImageChange}></uui-toggle>
                    <div slot="description">
                      If you would like Articulate to try and import the first image url in the post attachments
                    </div>
                  </uui-form-layout-item>
                </umb-form-validation-message>
                ${this._postCount !== undefined
                  ? html`
                      <uui-box>
                        <div>
                          <strong>Import file summary</strong>
                        </div>
                        <div class="import-summary-intro">
                          ${this._externalImageCount > 0
                            ? html`
                                This file references ${this._externalImageCount} external
                                image${this._externalImageCount === 1 ? '' : 's'} across ${this._externalHosts.length}
                                host${this._externalHosts.length === 1 ? '' : 's'}.
                              `
                            : html`This file does not reference any external image attachments.`}
                        </div>
                        ${this._externalHosts.length > 0
                          ? html`
                              <div class="import-hosts">
                                ${this._externalHosts.filter((host) => !this._blockedExternalHosts.includes(host))
                                  .length > 0
                                  ? html`
                                      <div class="import-hosts-heading">Allowed hosts</div>
                                      <div>
                                        ${this._externalHosts
                                          .filter((host) => !this._blockedExternalHosts.includes(host))
                                          .map(
                                            (host) => html`
                                              <uui-tag look="secondary" color="positive" class="import-host-tag">
                                                ${host}
                                              </uui-tag>
                                            `,
                                          )}
                                      </div>
                                    `
                                  : ''}
                                ${this._blockedExternalHosts.length > 0
                                  ? html`
                                      <div class="import-blocked-hosts-heading">Blocked hosts</div>
                                      <div>
                                        ${this._blockedExternalHosts.map(
                                          (host) => html`
                                            <uui-tag look="secondary" color="danger" class="import-host-tag">
                                              ${host}
                                            </uui-tag>
                                          `,
                                        )}
                                      </div>
                                    `
                                  : ''}
                              </div>
                            `
                          : ''}
                        ${this._blockedExternalHosts.length > 0
                          ? html`
                              <uui-box headline="Some external image hosts are not allowed" class="blocked-hosts-box">
                                ${this._importFirstImage
                                  ? html`
                                      Posts can still be imported, but external images from these hosts will not be
                                      fetched unless they are added to
                                      <code>Articulate:AllowedMediaHosts</code>. Import also validates any redirect
                                      targets, so all fetched hosts must be allowed.
                                    `
                                  : html`
                                      Posts can still be imported. This only matters if you enable
                                      <strong>Import First Image from Post Attachments</strong>. If you do, import will
                                      validate both the declared hosts and any redirect targets.
                                    `}
                                <div class="blocked-hosts-config">
                                  <div class="blocked-hosts-config-title">
                                    Hosts to add to Articulate:AllowedMediaHosts
                                  </div>
                                  <code class="blocked-hosts-list">${this._blockedExternalHosts.join('\n')}</code>
                                </div>
                              </uui-box>
                            `
                          : ''}
                      </uui-box>
                    `
                  : ''}
                <div class="form-actions">
                  ${this._isPreflighting
                    ? html`
                        <uui-tag look="secondary" color="warning" class="import-status-tag">
                          Analyzing BlogML file...
                        </uui-tag>
                      `
                    : ''}
                  ${this._postCount !== undefined && this._postCount > 0
                    ? html`
                        <uui-tag look="secondary" color="positive" class="import-status-tag">
                          ${this._postCount} posts in uploaded file.
                        </uui-tag>
                      `
                    : ''}
                  <uui-button
                    type="button"
                    look="outline"
                    color="default"
                    @click=${this.#verifySelectedFile}
                    ?disabled=${this._isPreflighting}
                    label="Verify file">
                    Verify file
                  </uui-button>
                  <uui-button
                    type="submit"
                    look="primary"
                    .state=${this._formState}
                    color="positive"
                    ?disabled=${!this._tempFileName || this._isPreflighting}
                    label="Submit">
                    Submit
                  </uui-button>
                  <uui-button type="button" look="secondary" @click=${this._handleReset} label="Reset">
                    Reset
                  </uui-button>
                </div>
              </form>
            `,
          )}
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
    css`
      .import-summary-intro {
        margin-top: 0.5rem;
      }

      .import-hosts {
        margin-top: 0.75rem;
      }

      .import-hosts-heading {
        font-size: 0.875rem;
        margin-bottom: 0.35rem;
      }

      .import-blocked-hosts-heading {
        font-size: 0.875rem;
        margin-top: 0.5rem;
        margin-bottom: 0.35rem;
      }

      .import-host-tag,
      .import-status-tag {
        margin-right: 0.5rem;
        margin-bottom: 0.5rem;
      }

      .blocked-hosts-box {
        margin-top: 0.75rem;
      }

      .blocked-hosts-config {
        margin-top: 0.75rem;
      }

      .blocked-hosts-config-title {
        font-size: 0.875rem;
        margin-bottom: 0.25rem;
      }

      .blocked-hosts-list {
        display: block;
        white-space: pre-wrap;
        user-select: all;
        padding: 0.75rem;
        border-radius: 6px;
        background: var(--uui-color-surface-alt);
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'blogml-importer': BlogMlImporterElement;
  }
}
