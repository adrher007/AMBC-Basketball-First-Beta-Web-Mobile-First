import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import {
  api,
  type AuthState,
  clearAuth,
  getStoredUser,
  storeAuth,
} from '../lib/api';

interface AuthContextValue {
  user: AuthState | null;
  login: (username: string, password: string, remember: boolean) => Promise<AuthState>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthState | null>(() => getStoredUser());

  async function login(username: string, password: string, remember: boolean) {
    const { data } = await api.post('/auth/login', { username, password });
    const auth: AuthState = {
      token: data.token,
      username: data.username,
      nombre: data.nombre,
      rol: data.rol,
    };
    storeAuth(auth, remember);
    setUser(auth);
    return auth;
  }

  function logout() {
    clearAuth();
    setUser(null);
  }

  // Valida el token al montar (si hay token guardado).
  useEffect(() => {
    if (!user) return;
    api
      .get('/auth/me')
      .catch(() => logout())
      .then((res) => {
        if (res && res.status !== 200) logout();
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}
