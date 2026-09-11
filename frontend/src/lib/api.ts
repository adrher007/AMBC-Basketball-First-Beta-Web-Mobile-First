import axios from 'axios';

export const api = axios.create({
  baseURL: '/api',
});

// Adjunta el token a cada request.
api.interceptors.request.use((config) => {
  const token = getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export interface AuthState {
  token: string;
  username: string;
  nombre: string;
  rol: string;
}

const TOKEN_KEY = 'ambc_token';
const USER_KEY = 'ambc_user';

export function getToken(): string | null {
  return (
    sessionStorage.getItem(TOKEN_KEY) ?? localStorage.getItem(TOKEN_KEY)
  );
}

export function getStoredUser(): AuthState | null {
  const raw = sessionStorage.getItem(USER_KEY) ?? localStorage.getItem(USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as AuthState;
  } catch {
    return null;
  }
}

// Recuérdame ON → localStorage (persiste 7 días, borrado manual)
// Recuérdame OFF → sessionStorage (se limpia al cerrar pestaña)
export function storeAuth(auth: AuthState, remember: boolean) {
  const storage = remember ? localStorage : sessionStorage;
  storage.setItem(TOKEN_KEY, auth.token);
  storage.setItem(USER_KEY, JSON.stringify(auth));
}

export function clearAuth() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(USER_KEY);
}