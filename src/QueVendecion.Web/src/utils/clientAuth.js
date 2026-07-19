const CLIENT_KEY_PREFIX = 'qvd_client_';

export function getClient(slug) {
  const raw = localStorage.getItem(CLIENT_KEY_PREFIX + slug);
  return raw ? JSON.parse(raw) : null;
}

export function setClient(slug, client) {
  localStorage.setItem(CLIENT_KEY_PREFIX + slug, JSON.stringify(client));
}

export function removeClient(slug) {
  localStorage.removeItem(CLIENT_KEY_PREFIX + slug);
}
