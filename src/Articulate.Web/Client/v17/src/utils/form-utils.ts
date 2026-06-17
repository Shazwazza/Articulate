import type { LitElement } from '@umbraco-cms/backoffice/external/lit';
import type { UUIButtonState } from '@umbraco-cms/backoffice/external/uui';

import { formatApiError } from './error-utils.js';

/**
 * Defines the contract for a LitElement-based component that manages form state.
 * Any component using the `setFormError` utility must implement this interface.
 * @interface IFormController
 */
export interface IFormController extends LitElement {
  _formState: UUIButtonState;
  _formError: { title: string; details: string[] } | null;

  /**
   * Resets component-specific state.
   * This is called by the form utility on error and can be used by the component for its own reset logic.
   * @param {boolean} [fullReset=false] If true, the component should also reset `_formState` and `_formError`.
   */
  resetState(fullReset?: boolean): void;
}

/**
 * A centralized utility to handle setting the error state on a form controller component.
 * @param {IFormController} host The component instance, which must implement `IFormController`.
 * @param {unknown} error The error object, which can be a standard `Error`, a custom object, or an API error.
 * @param {string} defaultMessage A fallback message to display if the error cannot be parsed.
 */
export function setFormError(host: IFormController, error: unknown, defaultMessage: string) {
  host._formState = 'failed';
  host._formError = formatApiError(error, defaultMessage);
  // Call the component's own method to reset any other specific state properties.
  host.resetState();
}
