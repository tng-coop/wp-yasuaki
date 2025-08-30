export async function storeCredentials(username, password) {
  if ('credentials' in navigator && window.PasswordCredential) {
    try {
      const cred = new PasswordCredential({ id: username, password: password });
      await navigator.credentials.store(cred);
    } catch {
      // ignore errors
    }
  }
}
