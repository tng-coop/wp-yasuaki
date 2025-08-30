export async function storeCredentials(username, password) {
  console.log('Storing credentials for user:', username);
  if ('credentials' in navigator && window.PasswordCredential) {
    try {
      console.log('Using Credential Management API to store credentials.');
      const cred = new PasswordCredential({ id: username, password: password });
      await navigator.credentials.store(cred);
    } catch {
      console.warn('Failed to store credentials using Credential Management API.');
      // ignore errors
    }
  }
}
