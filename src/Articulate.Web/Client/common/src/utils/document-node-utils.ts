import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import {
  type UmbDocumentItemModel,
  type UmbDocumentPickerModalData,
  type UmbDocumentPickerModalValue,
  UMB_DOCUMENT_PICKER_MODAL,
} from '@umbraco-cms/backoffice/document';
import {
  DocumentService,
  DocumentTypeService,
  type DocumentVariantResponseModel,
} from '@umbraco-cms/backoffice/external/backend-api';
import type { UmbModalContext, UmbModalManagerContext } from '@umbraco-cms/backoffice/modal';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
/**
 * Fetches a document variant by its UDI.
 * @param {UmbControllerHost} host The controller host used for the authenticated request.
 * @param {string} udi The UDI (Unique Data Identifier) of the document to fetch.
 * @returns {Promise<DocumentVariantResponseModel | null>} A promise that resolves to the first document variant, or null if not found or an error occurs.
 */
export async function documentById(host: UmbControllerHost, udi: string): Promise<DocumentVariantResponseModel | null> {
  const { data, error } = await tryExecute(host, DocumentService.getDocumentById({ path: { id: udi } }), {
    disableNotifications: true,
  });

  if (error) {
    console.error(`Failed to fetch ArticulateArchive node ${udi}`, error);
  }

  return data?.variants?.[0] ?? null;
}

/**
 * Fetches the UDI of the Articulate blog archive document type.
 * @param {UmbControllerHost} host The controller host used for the authenticated request.
 * @returns {Promise<string | undefined>} A promise that resolves to the UDI string of the blog archive document type, or undefined if not found or an error occurs.
 */
export async function articulateDocumentTypeKey(host: UmbControllerHost): Promise<string | undefined> {
  const { data, error } = await tryExecute(
    host,
    DocumentTypeService.getItemDocumentTypeSearch({
      query: { query: 'Articulate', skip: 0, take: 1, isElement: false },
    }),
    { disableNotifications: true },
  );

  if (error) {
    console.error('Failed to fetch Articulate document type', error);
  }

  return data?.items?.[0]?.id ?? undefined;
}

/**
 * Opens a document picker modal to select a node and returns its UDI.
 * @param {UmbModalManagerContext} modalManager The Umbraco modal manager context.
 * @param {string} doctypeUdi The UDI of the document type to filter the picker by.
 * @param {UmbControllerHost} host The controller host instance.
 * @returns {Promise<string | null>} A promise that resolves to the selected node's UDI, or null if no node is selected or an error occurs.
 */
export async function openNodePicker(
  modalManager: UmbModalManagerContext,
  doctypeUdi: string,
  host: UmbControllerHost,
): Promise<string | null> {
  try {
    // Keep parent nodes visible for navigation, but allow selecting only the archive type.
    const modalContext: UmbModalContext<UmbDocumentPickerModalData, UmbDocumentPickerModalValue> = modalManager.open<
      UmbDocumentPickerModalData,
      UmbDocumentPickerModalValue
    >(host, UMB_DOCUMENT_PICKER_MODAL, {
      data: {
        multiple: false,
        pickableFilter: (doc: UmbDocumentItemModel): boolean => {
          return doc.documentType?.unique === doctypeUdi;
        },
      },
    });
    const result = await modalContext.onSubmit();
    if (!result || !result.selection || !result.selection[0]) {
      return null;
    }
    return result.selection[0];
  } catch (error) {
    console.error(error, 'Node picker failed');
    return null;
  }
}
