/**
 * Cleans up a field name for display in the UI.
 * e.g., '$.articulateBlogNode' -> 'Articulate Blog Node'
 * @param {string} fieldName The raw field name.
 * @returns {string} A cleaned, human-readable field name.
 */
function cleanFieldName(fieldName) {
  const cleanedName = fieldName.startsWith('$.') ? fieldName.substring(2) : fieldName;
  return cleanedName.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (str) => str.toUpperCase());
}

function extractValidationMessages(problem) {
  if (!problem?.errors || typeof problem.errors !== 'object') {
    return [];
  }

  return Object.entries(problem.errors).flatMap(([key, messages]) => {
    const values = Array.isArray(messages) ? messages : [messages];
    const label = cleanFieldName(key);
    return values
      .filter((msg) => typeof msg === 'string' && msg.trim().length)
      .map((msg) => `${label}: ${msg.trim()}`);
  });
}

function normaliseDetails(value) {
  if (!value) {
    return [];
  }

  if (Array.isArray(value)) {
    return value
      .filter((msg) => typeof msg === 'string' && msg.trim().length)
      .map((msg) => msg.trim());
  }

  if (typeof value === 'string') {
    return value.trim().length ? [value.trim()] : [];
  }

  return [];
}

async function readResponsePayload(response) {
  try {
    const clone = response.clone();
    const contentType = clone.headers.get('content-type') || '';

    if (contentType.includes('application/json') || contentType.includes('+json')) {
      return await clone.json();
    }

    const text = await clone.text();
    return text && text.trim().length ? text.trim() : null;
  } catch (payloadError) {
    console.warn('[formatApiError] Failed to parse response payload', payloadError);
    return null;
  }
}

/**
 * Formats an API error into a user-friendly message object.
 * It can handle ProblemDetails objects, standard Error objects, fetch Responses and network failures.
 * @param {unknown} error The error object, which can be of any type.
 * @param {string} defaultMessage A fallback message if the error object is not recognized.
 * @returns {Promise<{title: string, details: string[], status?: number, isAuthError?: boolean, isForbidden?: boolean, isNetworkError?: boolean}>}
 */
export async function formatApiError(error, defaultMessage = 'An unexpected error occurred.') {
  console.warn('[formatApiError] Received error:', error);

  const meta = {
    status: typeof error?.status === 'number' ? error.status : undefined,
    isAuthError: Boolean(error?.isAuthError),
    isForbidden: Boolean(error?.isForbidden),
    isNetworkError: Boolean(error?.isNetworkError),
  };

  let payload = error;

  if (error instanceof Response) {
    meta.status = error.status;
    meta.isAuthError = meta.isAuthError || error.status === 401;
    meta.isForbidden = meta.isForbidden || error.status === 403;
    meta.isNetworkError = meta.isNetworkError || error.type === 'opaque';

    const parsed = await readResponsePayload(error);
    payload = parsed ?? { title: error.statusText || defaultMessage, detail: `HTTP ${error.status}` };
  }

  if (payload && typeof payload === 'object') {
    if (payload.problemDetails && typeof payload.problemDetails === 'object') {
      payload = payload.problemDetails;
    } else if (payload.details && typeof payload.details === 'object' && 'title' in payload.details) {
      payload = payload.details;
    }
  }

  const output = {
    title: defaultMessage,
    details: [],
    status: meta.status,
    isAuthError: meta.isAuthError,
    isForbidden: meta.isForbidden,
    isNetworkError: meta.isNetworkError,
  };

  if (payload && typeof payload === 'object' && 'title' in payload) {
    output.title = payload.title || output.title;
    const validationMessages = extractValidationMessages(payload);
    if (validationMessages.length) {
      output.details = validationMessages;
    } else if (typeof payload.detail === 'string' && payload.detail.trim().length) {
      output.details = [payload.detail.trim()];
    }
  } else if (payload instanceof Error) {
    if (typeof payload.status === 'number' && typeof output.status === 'undefined') {
      output.status = payload.status;
    }

    if (payload instanceof TypeError && payload.message === 'Failed to fetch') {
      output.isNetworkError = true;
    } else {
      output.isNetworkError = output.isNetworkError || Boolean(payload.isNetworkError);
    }

    output.isAuthError = output.isAuthError || Boolean(payload.isAuthError);
    output.isForbidden = output.isForbidden || Boolean(payload.isForbidden);

    if (payload.problemDetails && typeof payload.problemDetails === 'object') {
      const problem = payload.problemDetails;
      output.title = problem.title || output.title;
      const validationMessages = extractValidationMessages(problem);
      if (validationMessages.length) {
        output.details = validationMessages;
      } else if (typeof problem.detail === 'string' && problem.detail.trim().length) {
        output.details = [problem.detail.trim()];
      }
    }

    if (output.details.length === 0) {
      const detailCandidates = normaliseDetails(payload.details);
      if (detailCandidates.length) {
        output.details = detailCandidates;
      } else if (typeof payload.rawBody === 'string' && payload.rawBody.trim().length) {
        output.details = [payload.rawBody.trim()];
      } else if (typeof payload.message === 'string' && payload.message.trim().length) {
        output.details = [payload.message.trim()];
      }
    }
  } else if (typeof payload === 'string') {
    output.details = payload.trim().length ? [payload.trim()] : [];
  } else if (payload && typeof payload === 'object' && typeof payload.message === 'string') {
    output.details = payload.message.trim().length ? [payload.message.trim()] : [];
  }

  if (!Array.isArray(output.details)) {
    output.details = [];
  }

  output.details = Array.from(new Set(output.details.filter((msg) => typeof msg === 'string' && msg.trim().length))).map((msg) => msg.trim());

  if (output.isAuthError || output.status === 401) {
    output.isAuthError = true;
    output.status = output.status ?? 401;
    const currentTitle = output.title?.toLowerCase() || '';
    if (!output.title || output.title === defaultMessage || currentTitle === 'unauthorized') {
      output.title = 'Authentication required';
    }
    if (output.details.length === 0) {
      output.details = ['Your Umbraco session has expired or is missing. Please sign in again to continue.'];
    }
  } else if (output.isForbidden || output.status === 403) {
    output.isForbidden = true;
    output.status = output.status ?? 403;
    const currentTitle = output.title?.toLowerCase() || '';
    if (!output.title || output.title === defaultMessage || currentTitle === 'forbidden') {
      output.title = 'Insufficient permissions';
    }
    if (output.details.length === 0) {
      output.details = ['You do not have permission to publish posts for this Articulate archive. Request Create and Publish rights in the Umbraco back office.'];
    }
  } else if (output.isNetworkError) {
    if (!output.title || output.title === defaultMessage) {
      output.title = 'Network error';
    }
    if (output.details.length === 0) {
      output.details = ['Could not connect to the server. Please verify your connection and try again.'];
    }
  }

  if (output.details.length === 0) {
    output.details = [defaultMessage];
  }

  return output;
}
