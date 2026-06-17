import { customElement, html, property, query, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUIButtonState } from '@umbraco-cms/backoffice/external/uui';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { type UmbModalManagerContext, UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { UmbValidationContext } from '@umbraco-cms/backoffice/validation';
import { keyed } from 'lit-html/directives/keyed.js';
import { type UmbAuthContext, UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { BlogMlService } from '../api/sdk.gen.js';
import type { ImportFileResponse, ImportModel, ImportResponse } from '../api/types.gen.js';
import { ArticulateDocumentTypeKey, DocumentById, openNodePicker } from '../utils/document-node-utils.js';
import { type IFormController, setFormError } from '../utils/form-utils.js';
import { showUmbracoNotification } from '../utils/notification-utils.js';
import { renderErrorMessage, renderHeaderActions } from '../utils/template-utils.js';
import { BoxStyles, ErrorBoxStyles, FormStyles, HostStyles, NodePickerStyles } from '../utils/style-utils.js';

/**
 * A LitElement-based component for importing blog content from a BlogML file.
 *
 * @element blogml-importer
 * @extends UmbLitElement
 * @implements {IFormController}
 */
@customElement('blogml-importer')
export default class BlogMlImporterElement extends UmbLitElement implements IFormController {
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
   * The UDI of the selected Articulate blog node for import.
   * @private
   * @type {string | undefined}
   */
  @state() private _articulateBlogNode: string | undefined = undefined;
  /**
   * The name of the selected blog node, displayed in the input.
   * @private
   * @type {string}
   */
  @state() private _selectedBlogNodeName: string = '';
  /**
   * The number of posts found in the uploaded BlogML file.
   * @private
   * @type {number | undefined}
   */
  @state() private _postCount: number | undefined = undefined;
  /**
   * The number of external image attachments found in the uploaded BlogML file.
   * @private
   * @type {number}
   */
  @state() private _externalImageCount = 0;
  /**
   * The unique external hosts referenced by the uploaded BlogML file.
   * @private
   * @type {string[]}
   */
  @state() private _externalHosts: string[] = [];
  /**
   * The external hosts referenced by the uploaded BlogML file that are not currently allowlisted.
   * @private
   * @type {string[]}
   */
  @state() private _blockedExternalHosts: string[] = [];
  /**
   * The temporary file name returned by preflight for the currently selected BlogML file.
   * @private
   * @type {string | undefined}
   */
  @state() private _tempFileName: string | undefined = undefined;
  /**
   * Whether the "import first image" option is currently enabled in the form.
   * @private
   * @type {boolean}
   */
  @state() private _importFirstImage = false;
  /**
   * Whether the selected BlogML file is currently being analyzed.
   * @private
   * @type {boolean}
   */
  @state() private _isPreflighting = false;
  /**
   * Monotonically increasing token used to ignore stale async file-analysis results.
   * @private
   * @type {number}
   */
  @state() private _analysisRequestId = 0;
  /**
   * A key to force re-rendering of the form, used for resetting the file input.
   * @private
   * @type {number}
   */
  @state() private _formRenderKey = 0;

  /**
   * The main form element.
   * @private
   * @type {HTMLFormElement}
   */
  @query('#blogMlImportForm')
  private _form!: HTMLFormElement;

  /**
   * The modal manager context, used for opening the node picker.
   * @private
   * @type {UmbModalManagerContext | undefined}
   */
  private _modalManagerContext?: UmbModalManagerContext;
  /**
   * The auth context, used for getting the auth token.
   * @private
   * @type {UmbAuthContext | undefined}
   */
  private _authContext?: UmbAuthContext;
  /**
   * The UDI of the Articulate Archive document type.
   * @private
   * @type {string | undefined}
   */
  private _archiveDoctypeUdi: string | undefined = undefined;

  #validation = new UmbValidationContext(this);

  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (instance) => {
      this._modalManagerContext = instance;
    });
    this.consumeContext(UMB_AUTH_CONTEXT, (instance) => {
      this._authContext = instance;
    });
  }

  /**
   * Fetches the Articulate Archive doctype UDI when the component connects.
   * @async
   */
  async connectedCallback() {
    super.connectedCallback();
    this._archiveDoctypeUdi = await ArticulateDocumentTypeKey(this._authContext!);
    if (this._archiveDoctypeUdi === null) {
      const error = new Error(
        'Could not find the Articulate Archive document type. Please ensure Articulate is installed correctly.',
      );
      error.name = 'Configuration Error';
      setFormError(this, error, error.name);
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
      // Incrementing the key forces Lit to re-render the form from scratch, effectively resetting it.
      // workaround for dirty uui-input-file after form submission and reset
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
      const variant = await DocumentById(this._authContext!, udi);
      if (!variant) {
        setFormError(this, new Error(`Could not find a node with UDI: ${udi}`), 'Node Not Found');
        return;
      }
      this._articulateBlogNode = udi;
      this._selectedBlogNodeName = variant.name;
    }
  }

  /**
   * Triggers a browser download for a given Blob.
   * @param {Blob} blob The file blob to download.
   * @param {string} fileName The name for the downloaded file.
   */
  #downloadFile = (blob: Blob, fileName: string) => {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.style.display = 'none';
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    // Dispatch a non-bubbling click so the Umbraco backoffice router does not
    // intercept the anchor and try to navigate to the blob: URL via pushState.
    a.dispatchEvent(
      new MouseEvent('click', {
        bubbles: false,
        cancelable: true,
        composed: false,
        view: window,
      }),
    );

    // Delay cleanup so the browser has time to start the download before the
    // object URL is revoked and the anchor is removed.
    window.setTimeout(() => {
      window.URL.revokeObjectURL(url);
      a.remove();
    }, 1000);
  };

  /**
   * Type guard to check if a value is a Blob.
   * @param {unknown} value The value to check.
   * @returns {boolean} True if the value is a Blob.
   */
  #isBlob = (value: unknown): value is Blob => {
    return value instanceof Blob;
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
      typeof (data as ImportFileResponse).postCount === 'number' &&
      'externalImageCount' in data &&
      typeof (data as ImportFileResponse).externalImageCount === 'number' &&
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
      'authorCount' in data &&
      'commentCount' in data &&
      'completed' in data
    );
  };

  #deleteTempFile = async () => {
    if (!this._tempFileName) return;

    try {
      await BlogMlService.deleteBlogmlImportFile({ query: { tempFile: this._tempFileName } });
    } catch {
      // Best-effort cleanup for abandoned preflight uploads.
    } finally {
      this._tempFileName = undefined;
    }
  };

  #getSelectedImportFile = (): File | undefined => {
    if (!this._form) {
      return undefined;
    }

    const importFile = new FormData(this._form).get('importFile');
    return importFile instanceof File && importFile.size > 0 ? importFile : undefined;
  };

  #handleImportFileChange = async () => {
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
      setFormError(this, validationError, validationError.name);
      return;
    }

    this._isPreflighting = true;

    try {
      const initData = await this.#beginImport(importFile);
      if (requestId !== this._analysisRequestId) {
        await BlogMlService.deleteBlogmlImportFile({ query: { tempFile: initData.temporaryFileName } });
        return;
      }

      this._postCount = initData.postCount;
      this._externalImageCount = initData.externalImageCount;
      this._externalHosts = initData.externalHosts;
      this._blockedExternalHosts = initData.blockedExternalHosts;
      this._tempFileName = initData.temporaryFileName;
    } catch (error) {
      if (requestId !== this._analysisRequestId) {
        return;
      }

      setFormError(this, error, 'Import Analysis Failed');
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
        await BlogMlService.deleteBlogmlImportFile({ query: { tempFile: initData.temporaryFileName } });
        return;
      }

      this._postCount = initData.postCount;
      this._externalImageCount = initData.externalImageCount;
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

    try {
      await this.#validation.validate();
    } catch (error) {
      setFormError(this, error, 'Validation Failed');
      return;
    }

    const formData = new FormData(this._form);
    const importFile = formData.get('importFile') as File;

    // validate() does not appear to work with the uui-file-input form element consistently or the node picker, so backup validation
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
      setFormError(this, validationError, validationError.name);
      return;
    }

    if (this._formState === 'waiting') return;

    this._formState = 'waiting';
    this._formError = null;

    try {
      const importData = await this.#finalizeImport(formData, this._tempFileName!);
      if (formData.get('exportDisqusXml') === 'on' && importData.commentCount > 0) {
        await this.#exportDisqusComments();
      }

      this._formState = 'success';
      const disqusMessage =
        formData.get('exportDisqusXml') === 'on' && importData.commentCount > 0
          ? `${importData.commentCount} comments exported.`
          : formData.get('exportDisqusXml') === 'on'
            ? 'No comments found to export.'
            : '';

      await showUmbracoNotification(
        this,
        `BlogML imported successfully! ${importData.authorCount} authors, ${importData.postCount} posts imported. ${disqusMessage}`,
        'positive',
        true,
      );
      this.resetState(true);
    } catch (error) {
      this._tempFileName = undefined;
      if (importFile && importFile.size > 0) {
        void this.#restoreTempFileForRetry(importFile);
      }
      setFormError(this, error, 'Import Failed');
    }
  };

  /**
   * Begins the import process by uploading the BlogML file to the server.
   * @param {File} importFile The BlogML file to upload.
   * @returns {Promise<PostArticulateBlogmlImportFileResponse>} A promise that resolves with the initial import data, including the temporary file name and post count.
   * @private
   * @async
   */
  #beginImport = async (importFile: File): Promise<ImportFileResponse> => {
    const result = await BlogMlService.postBlogmlImportFile({ body: { importFile } });

    if (!result.response.ok || !this.#isImportFileResponse(result.data)) {
      throw result.error || new Error('The server returned an invalid response when uploading the file.');
    }

    // Now that the type is confirmed, we can safely validate the content.
    if (!result.data.temporaryFileName || result.data.postCount <= 0) {
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
    const result = await BlogMlService.postBlogmlImport({ body: payload });

    if (!result.response.ok || !this.#isImportResponse(result.data)) {
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
    const result = await BlogMlService.getBlogmlExportDisqus();
    if (!result.response.ok || !result.data) {
      throw result.error || new Error('Failed to export Disqus comments.');
    }
    const blob = result.data;
    if (!this.#isBlob(blob)) {
      throw new Error('Invalid file received for Disqus export.');
    }
    const contentDisposition = result.response.headers.get('content-disposition');
    let fileName = 'disqus-comments.xml'; // Default filename
    if (contentDisposition) {
      const fileNameMatch = contentDisposition.match(/filename\*="UTF-8''([^"]+)"/);
      if (fileNameMatch && fileNameMatch.length > 1 && fileNameMatch[1]) {
        fileName = fileNameMatch[1];
      } else {
        const fileNameMatch = contentDisposition.match(/filename="?([^"]+)"?/);
        if (fileNameMatch && fileNameMatch.length > 1 && fileNameMatch[1]) {
          fileName = fileNameMatch[1];
        }
      }
    }
    this.#downloadFile(blob, fileName);
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
                <uui-form-validation-message>
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
                        style="flex-grow: 1;"></uui-input>
                      <uui-button
                        look="outline"
                        label=${this._articulateBlogNode ? 'Change' : 'Choose'}
                        @click=${this._openNodePicker}></uui-button>
                    </div>
                    <div slot="description">Choose the Articulate blog node to export from</div>
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
                      style="--auto-width-text-margin-right: 20px"
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
                      style="--auto-width-text-margin-right: 20px"
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
                </uui-form-validation-message>
                ${this._postCount !== undefined
                  ? html`
                      <uui-box>
                        <div>
                          <strong>Import file summary</strong>
                        </div>
                        <div style="margin-top: 0.5rem;">
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
                              <div style="margin-top: 0.75rem;">
                                ${this._externalHosts.filter((host) => !this._blockedExternalHosts.includes(host))
                                  .length > 0
                                  ? html`
                                      <div style="font-size: 0.875rem; margin-bottom: 0.35rem;">Allowed hosts</div>
                                      <div>
                                        ${this._externalHosts
                                          .filter((host) => !this._blockedExternalHosts.includes(host))
                                          .map(
                                            (host) => html`
                                              <uui-tag
                                                look="secondary"
                                                color="positive"
                                                style="margin-right: 0.5rem; margin-bottom: 0.5rem;">
                                                ${host}
                                              </uui-tag>
                                            `,
                                          )}
                                      </div>
                                    `
                                  : ''}
                                ${this._blockedExternalHosts.length > 0
                                  ? html`
                                      <div style="font-size: 0.875rem; margin-top: 0.5rem; margin-bottom: 0.35rem;">
                                        Blocked hosts
                                      </div>
                                      <div>
                                        ${this._blockedExternalHosts.map(
                                          (host) => html`
                                            <uui-tag
                                              look="secondary"
                                              color="danger"
                                              style="margin-right: 0.5rem; margin-bottom: 0.5rem;">
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
                              <uui-box
                                headline="Some external image hosts are not allowed"
                                style="margin-top: 0.75rem;">
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
                                <div style="margin-top: 0.75rem;">
                                  <div style="font-size: 0.875rem; margin-bottom: 0.25rem;">
                                    Hosts to add to Articulate:AllowedMediaHosts
                                  </div>
                                  <code
                                    style="display: block; white-space: pre-wrap; user-select: all; padding: 0.75rem; border-radius: 6px; background: var(--uui-color-surface-alt);"
                                    >${this._blockedExternalHosts.join('\n')}</code
                                  >
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
                        <uui-tag look="secondary" color="warning" style="margin-right: 1em;">
                          Analyzing BlogML file...
                        </uui-tag>
                      `
                    : ''}
                  ${this._postCount !== undefined && this._postCount > 0
                    ? html`
                        <uui-tag look="secondary" color="positive" style="margin-right: 1em;">
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
                    color="primary"
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
    'blogml-importer': BlogMlImporterElement;
  }
}
