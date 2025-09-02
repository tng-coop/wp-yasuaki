import { getNonce } from './wpNonce.js';

export async function apiFetch(path, options = {}) {
  const authMode = new URLSearchParams(window.location.search).get('auth');
  const useNonce = authMode === 'nonce';
  const baseUrl = localStorage.getItem('wpEndpoint') || '';
  const url = path.startsWith('http') ? path : baseUrl.replace(/\/$/, '') + path;
  options.headers = options.headers || {};
  if (useNonce) {
    const nonce = await getNonce();
    if (!nonce) {
      console.error('Failed to retrieve WP nonce for auth');
      throw new Error('Authentication failed (no nonce)');
    }
    options.headers['X-WP-Nonce'] = nonce;
    options.credentials = 'include';
  } else {
    const token = localStorage.getItem('jwtToken');
    if (token) {
      options.headers['Authorization'] = 'Bearer ' + token;
    }
  }
  const response = await fetch(url, options);
  return response;
}

window.apiFetch = apiFetch;
