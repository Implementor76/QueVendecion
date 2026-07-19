const AUTH_KEY = 'qv_auth';

export function getAuth() {
  const data = localStorage.getItem(AUTH_KEY);
  return data ? JSON.parse(data) : null;
}

export function setAuth(data) {
  localStorage.setItem(AUTH_KEY, JSON.stringify(data));
}

export function clearAuth() {
  localStorage.removeItem(AUTH_KEY);
}

export function isLoggedIn() {
  return getAuth() !== null;
}

export function getToken() {
  return getAuth()?.token;
}

export function getTiendaId() {
  return getAuth()?.tiendaId;
}

export function getUserName() {
  return getAuth()?.nombre;
}
