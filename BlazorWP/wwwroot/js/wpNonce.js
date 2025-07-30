export async function getNonce() {
  try {
    const nonce = await fetch('/wp-admin/admin-ajax.php?action=rest-nonce', {
      credentials: 'same-origin'
    }).then(r => r.text());
    if (!nonce) return null;
    await fetch('/wp-json/wp/v2/users/me', {
      method: 'GET',
      credentials: 'same-origin',
      headers: { 'X-WP-Nonce': nonce }
    }).then(r => r.json());
    return nonce;
  } catch (err) {
    console.log('Failed to retrieve nonce', err);
    return null;
  }
}
