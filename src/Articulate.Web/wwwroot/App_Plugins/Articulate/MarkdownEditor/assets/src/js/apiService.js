import { config } from './config.js';
import { authService } from './authService.js';

/**
 * Creates a new blog post by sending data to the backend API.
 * @param {object} postData The post data (title, body, etc.).
 * @param {Map<string, File>} fileMap A map of temporary URLs to File objects for images.
 * @returns {Promise<object>} The JSON response from the server.
 */
async function createPost(postData, fileMap) {
  const token = await authService.getAccessToken();
  if (!token) {
    const error = new Error('Session expired. Please log in again.');
    error.isAuthError = true;
    throw error;
  }
  const formData = new FormData();
  formData.append('json', JSON.stringify(postData));


  for (const [key, file] of fileMap.entries()) {
    formData.append(key, file);
  }


  const requestOptions = {
    method: 'POST',
    body: formData,
    headers: {
      'Authorization': `Bearer ${token}`,
    },
  };
  const response = await fetch(config.editorPostUrl, requestOptions);

  if (!response.ok) {
    // Defer parsing/normalisation to error-formatter
    throw response;
  }

  return response.json();
}

async function getCurrentUser() {
  const token = await authService.getAccessToken();
  if (!token) {
    const error = new Error('User not authenticated');
    error.isAuthError = true;
    throw error;
  }

  const requestOptions = {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  };


  const response = await fetch(config.currentUserUrl, requestOptions);

  if (!response.ok) {
    // Defer parsing/normalisation to error-formatter
    throw response;
  }

  return response.json();
}

export const apiService = {
  createPost,
  getCurrentUser,
};
