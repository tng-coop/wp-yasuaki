import { getNonce } from './wpNonce.js';

export async function apiFetch(path, options = {}) {
  const authMode = new URLSearchParams(window.location.search).get('auth');
  const useNonce = authMode === 'nonce';
  const baseUrl = new URLSearchParams(window.location.search).get('wpurl') || '';
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
    const user = localStorage.getItem('app_user');
    const pass = localStorage.getItem('app_pass');
    if (user && pass) {
      const basic = btoa(`${user}:${pass}`);
      options.headers['Authorization'] = 'Basic ' + basic;
      options.credentials = 'omit'; // 👈 ignore cookies so Basic is honored
    }
  }
  const response = await fetch(url, options);
  return response;
}

window.apiFetch = apiFetch;
