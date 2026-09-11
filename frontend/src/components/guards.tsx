import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

// Destino post-login según el rol
export function destinoPorRol(rol: string): string {
  switch (rol) {
    case 'ADMIN_AMBC': return '/admin';
    case 'COACH': return '/entrenador';
    case 'ARBITRO': return '/arbitro';
    default: return '/inicio';
  }
}

// Exige sesión iniciada. Opcional: restringir a uno o más roles.
export function RequireAuth({ roles }: { roles?: string[] }) {
  const { user } = useAuth();
  const location = useLocation();

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (roles && !roles.includes(user.rol)) {
    return <Navigate to="/inicio" replace />;
  }

  return <Outlet />;
}

// Redirige al destino del rol si ya hay sesión (evita volver a /login).
export function GuestOnly() {
  const { user } = useAuth();
  return user ? <Navigate to={destinoPorRol(user.rol)} replace /> : <Outlet />;
}
