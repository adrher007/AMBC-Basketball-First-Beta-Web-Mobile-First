import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import './AdminDashboard.module.css';
import Card from '../../components/Card';
import Badge from '../../components/Badge';
import { LoadingState, ErrorState } from '../../components/States';
import { getDashboard, type DashboardData } from '../../lib/dashboard';
import {
  CalendarDays,
  Users,
  UserCircle,
  ClipboardList,
  Trophy,
  ShieldCheck,
  Bell,
} from 'lucide-react';

export default function AdminDashboard() {
  const navigate = useNavigate();
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  function load() {
    setLoading(true);
    setError(false);
    getDashboard()
      .then(setData)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  const accesos = [
    { to: '/admin/temporada', label: 'Temporada', icon: <CalendarDays /> },
    { to: '/admin/equipos', label: 'Equipos', icon: <Users /> },
    { to: '/admin/entrenadores', label: 'Entrenadores', icon: <UserCircle /> },
    { to: '/admin/arbitros', label: 'Árbitros', icon: <ClipboardList /> },
    { to: '/admin/partidos/programar', label: 'Programar partidos', icon: <Trophy /> },
    { to: '/admin/resultados', label: 'Resultados', icon: <ShieldCheck /> },
    { to: '/admin/playoffs', label: 'Playoffs', icon: <Trophy /> },
    { to: '/admin/avisos', label: 'Avisos', icon: <Bell /> },
  ];

  const stats = data
    ? [
        { label: 'Equipos', value: data.totalEquipos },
        { label: 'Jugadores', value: data.totalJugadores },
        { label: 'Entrenadores', value: data.totalCoaches },
        { label: 'Árbitros', value: data.totalArbitros },
        { label: 'Partidos', value: data.totalPartidos },
        { label: 'Pendientes', value: data.partidosPendientes },
      ]
    : [];

  return (
    <div className="page">
      <h1>Panel de administración</h1>

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="el panel" onRetry={load} />}

      {!loading && !error && data && (
        <>
          {/* Temporada activa */}
          <Card>
            <div className="season">
              {data.temporadaActiva ? (
                <>
                  <div className="season-title">{data.temporadaActiva.nombre}</div>
                  <Badge color="success">EN CURSO</Badge>
                </>
              ) : (
                <>
                  <div className="season-title">Sin temporada activa</div>
                  <Badge color="default">SIN INICIAR</Badge>
                </>
              )}
            </div>
          </Card>

          {/* Métricas */}
          <div className="stats-grid">
            {stats.map((s) => (
              <Card key={s.label}>
                <div className="stat">
                  <div className="stat-value">{s.value}</div>
                  <div className="stat-label">{s.label}</div>
                </div>
              </Card>
            ))}
          </div>

          {/* Accesos rápidos */}
          <h2 className="section-title">Accesos rápidos</h2>
          <div className="accesos-grid">
            {accesos.map((a) => (
              <Card key={a.to} onClick={() => navigate(a.to)}>
                <div className="acceso">
                  <span className="acceso-icon">{a.icon}</span>
                  <span className="acceso-label">{a.label}</span>
                </div>
              </Card>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
