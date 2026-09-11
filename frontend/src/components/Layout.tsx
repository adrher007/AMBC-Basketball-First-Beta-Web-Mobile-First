import './Layout.module.css';
import { useEffect, useState } from 'react';
import { NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom';
import {
  Home,
  Calendar,
  Users,
  Trophy,
  Swords,
  Bell,
  MessageCircle,
  Sun,
  Moon,
  LogOut,
  LogIn,
  ShieldCheck,
  UserCircle,
  ClipboardList,
  MoreHorizontal,
  UserRound,
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { api } from '../lib/api';

interface NavItem {
  to: string;
  label: string;
  icon: React.ReactNode;
}

export default function Layout() {
  const { user, logout } = useAuth();
  const { theme, toggle } = useTheme();
  const navigate = useNavigate();
  const location = useLocation();

  // ===== Menús por rol (sidebar desktop) =====
  const menuAdmin: NavItem[] = [
    { to: '/admin', label: 'Panel Admin', icon: <ShieldCheck /> },
    { to: '/admin/temporada', label: 'Temporada', icon: <Calendar /> },
    { to: '/admin/equipos', label: 'Equipos', icon: <Users /> },
    { to: '/admin/arbitros', label: 'Árbitros', icon: <ClipboardList /> },
    { to: '/admin/entrenadores', label: 'Entrenadores', icon: <UserCircle /> },
    { to: '/admin/partidos', label: 'Partidos', icon: <Trophy /> },
    { to: '/admin/partidos/programar', label: 'Programar semana', icon: <Calendar /> },
    { to: '/admin/resultados', label: 'Resultados', icon: <Trophy /> },
  ];

  const menuCoach: NavItem[] = [
    { to: '/entrenador', label: 'Mi Equipo', icon: <UserCircle /> },
    { to: '/entrenador/plantilla', label: 'Plantilla', icon: <Users /> },
  ];

  const menuArbitro: NavItem[] = [
    { to: '/arbitro', label: 'Partidos Asignados', icon: <Calendar /> },
    { to: '/arbitro/planillas', label: 'Planillas Activas', icon: <ClipboardList /> },
  ];

  const menuFan: NavItem[] = [
    { to: '/inicio', label: 'Inicio', icon: <Home /> },
    { to: '/partidos', label: 'Partidos', icon: <Calendar /> },
    { to: '/equipos', label: 'Equipos', icon: <Users /> },
    { to: '/torneo', label: 'Torneo', icon: <Trophy /> },
    { to: '/playoffs', label: 'Playoffs', icon: <Swords /> },
    { to: '/notificaciones', label: 'Avisos', icon: <Bell /> },
    { to: '/chat', label: 'Chat', icon: <MessageCircle /> },
  ];

  const secundario: NavItem[] = [
    { to: '/partidos', label: 'Partidos', icon: <Calendar /> },
    { to: '/clasificacion', label: 'Clasificación', icon: <Trophy /> },
  ];

  const principal: NavItem[] = user
    ? user.rol === 'ADMIN_AMBC' ? menuAdmin
      : user.rol === 'COACH' ? menuCoach
      : user.rol === 'ARBITRO' ? menuArbitro
      : menuFan
    : menuFan;

  const mostrarSecundario = user && user.rol !== 'ADMIN_AMBC' && user.rol !== 'FAN';

  // ===== TabBar móvil por rol (HIG iOS) =====
  const tabFan: NavItem[] = [
    { to: '/inicio', label: 'Home', icon: <Home /> },
    { to: '/partidos', label: 'Calendario', icon: <Calendar /> },
    { to: '/equipos', label: 'Equipos', icon: <Users /> },
    { to: '/torneo', label: 'Torneo', icon: <Trophy /> },
    { to: '/mas', label: 'Más', icon: <MoreHorizontal /> },
  ];

  const tabAdmin: NavItem[] = [
    { to: '/admin', label: 'Panel', icon: <ShieldCheck /> },
    { to: '/equipos', label: 'Equipos', icon: <Users /> },
    { to: '/torneo', label: 'Torneo', icon: <Trophy /> },
    { to: '/mas', label: 'Más', icon: <MoreHorizontal /> },
  ];

  const tabCoach: NavItem[] = [
    { to: '/entrenador', label: 'Mi Equipo', icon: <UserCircle /> },
    { to: '/entrenador/plantilla', label: 'Plantilla', icon: <Users /> },
    { to: '/torneo', label: 'Torneo', icon: <Trophy /> },
    { to: '/mas', label: 'Más', icon: <MoreHorizontal /> },
  ];

  const tabArbitro: NavItem[] = [
    { to: '/arbitro', label: 'Mis partidos', icon: <Calendar /> },
    { to: '/arbitro/planillas', label: 'Planillas', icon: <ClipboardList /> },
    { to: '/torneo', label: 'Torneo', icon: <Trophy /> },
    { to: '/mas', label: 'Más', icon: <MoreHorizontal /> },
  ];

  const tabs: NavItem[] = user
    ? user.rol === 'ADMIN_AMBC' ? tabAdmin
      : user.rol === 'COACH' ? tabCoach
      : user.rol === 'ARBITRO' ? tabArbitro
      : tabFan
    : tabFan;

  function handleLogout() {
    logout();
    navigate('/login');
  }

  useEffect(() => {
    document.title = 'AMBC Basketball';
  }, []);

  // ===== Contador real de notificaciones no leídas =====
  // Solo para usuarios con sesión (FAN no tiene usuario en BD, sus avisos son públicos).
  const [noLeidas, setNoLeidas] = useState(0);

  useEffect(() => {
    if (!user) {
      setNoLeidas(0);
      return;
    }
    api
      .get<{ vista: boolean }[]>('/notificaciones')
      .then((r) => setNoLeidas(r.data.filter((n) => !n.vista).length))
      .catch(() => setNoLeidas(0));
  }, [user, location.pathname]);

  // Título del header según la ruta
  const tituloHeader =
    location.pathname === '/' ? 'AMBC'
      : location.pathname.includes('/admin') ? 'Panel'
      : location.pathname.includes('/entrenador') ? 'Mi Equipo'
      : location.pathname.includes('/arbitro') ? 'Árbitro'
      : location.pathname.includes('/partidos') ? 'Partidos'
      : location.pathname.includes('/equipos') ? 'Equipos'
      : location.pathname.includes('/torneo') ? 'Torneo'
      : location.pathname.includes('/clasificacion') ? 'Clasificación'
      : location.pathname.includes('/playoffs') ? 'Playoffs'
      : location.pathname.includes('/notificaciones') ? 'Avisos'
      : location.pathname.includes('/chat') ? 'Chat'
      : location.pathname.includes('/mas') ? 'Más'
      : 'AMBC';

  return (
    <div className="layout">
      {/* Sidebar (desktop) */}
      <aside className="sidebar">
        <div className="brand">AMBC</div>
        <nav className="nav" aria-label="Navegación principal">
          {principal.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `nav-item ${isActive ? 'nav-active' : ''}`
              }
            >
              {item.icon}
              <span className="nav-label">{item.label}</span>
            </NavLink>
          ))}

          {mostrarSecundario && (
            <>
              <div className="nav-sep" />
              {secundario.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) =>
                    `nav-item nav-secondary ${isActive ? 'nav-active' : ''}`
                  }
                >
                  {item.icon}
                  <span className="nav-label">{item.label}</span>
                </NavLink>
              ))}
            </>
          )}
        </nav>
        <div className="sidebar-footer">
          {!user && (
            <span className="fan-badge">
              <Users size={14} /> Aficionado
            </span>
          )}
          <button className="icon-btn" onClick={toggle} aria-label="Tema">
            {theme === 'light' ? <Moon size={20} /> : <Sun size={20} />}
          </button>
          {user ? (
            <button className="icon-btn" onClick={handleLogout} aria-label="Salir">
              <LogOut size={20} />
            </button>
          ) : (
            <button className="icon-btn" onClick={() => navigate('/login')} aria-label="Entrar">
              <LogIn size={20} />
            </button>
          )}
        </div>
      </aside>

      {/* Contenido */}
      <main className="content" id="main-content">
        {/* Header móvil: título + notificaciones + avatar */}
        <header className="mobile-header">
          <h1 className="mobile-title">{tituloHeader}</h1>
          <div className="mobile-actions">
            <button
              className="icon-btn"
              onClick={() => navigate('/notificaciones')}
              aria-label={`Notificaciones${noLeidas > 0 ? ` (${noLeidas} sin leer)` : ''}`}
            >
              <Bell size={20} />
              {noLeidas > 0 && <span className="notif-dot">{noLeidas > 9 ? '9+' : noLeidas}</span>}
            </button>
            <button
              className="avatar-btn"
              onClick={() => navigate(user ? '/mas' : '/login')}
              aria-label="Avatar"
            >
              <UserRound size={22} />
            </button>
          </div>
        </header>

        <Outlet />
      </main>

      {/* TabBar flotante (móvil) */}
      <nav className="tabbar" aria-label="Navegación móvil">
        {tabs.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              `tabbar-item ${isActive ? 'tabbar-active' : ''}`
            }
          >
            {item.icon}
            <span className="tabbar-label">{item.label}</span>
          </NavLink>
        ))}
      </nav>
    </div>
  );
}
