import { useEffect, useState } from 'react';
import './Notificaciones.module.css';
import Card from '../components/Card';
import Badge from '../components/Badge';
import { LoadingState, EmptyState, ErrorState } from '../components/States';
import { getNotificaciones, marcarNotificacionVista, getNotificacionesFan } from '../lib/notificaciones';
import { useAuth } from '../context/AuthContext';
import type { Notification } from '../lib/types';

export default function Notificaciones() {
  const { user } = useAuth();
  const [notifs, setNotifs] = useState<Notification[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const isFan = !user; // sin login = FAN

  function load() {
    setLoading(true);
    setError(false);
    const fn = isFan ? getNotificacionesFan : getNotificaciones;
    fn()
      .then((list) => {
        if (isFan) {
          // FAN: "visto" por localStorage
          const vistos = JSON.parse(localStorage.getItem('ambc_notif_vistas') || '[]');
          setNotifs(list.map((n) => ({ ...n, vista: vistos.includes(n.id) })));
        } else {
          setNotifs(list);
        }
      })
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  async function handleVista(n: Notification) {
    if (isFan) {
      const vistos = JSON.parse(localStorage.getItem('ambc_notif_vistas') || '[]');
      if (!vistos.includes(n.id)) {
        vistos.push(n.id);
        localStorage.setItem('ambc_notif_vistas', JSON.stringify(vistos));
      }
      setNotifs((prev) => prev.map((x) => (x.id === n.id ? { ...x, vista: true } : x)));
      return;
    }
    await marcarNotificacionVista(n.id);
    setNotifs((prev) => prev.map((x) => (x.id === n.id ? { ...x, vista: true } : x)));
  }

  function formatFecha(iso: string) {
    return new Date(iso).toLocaleString('es-BO', {
      day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit',
    });
  }

  return (
    <div className="page">
      <h1>Avisos</h1>

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los avisos" onRetry={load} />}
      {!loading && !error && notifs.length === 0 && <EmptyState label="avisos" />}

      {!loading && !error && notifs.length > 0 && (
        <div className="list">
          {notifs.map((n) => (
            <Card key={n.id} onClick={() => !n.vista && handleVista(n)}>
              <div className="notif">
                <div className="notif-body">
                  <div className="notif-msg">{n.mensaje}</div>
                  <div className="notif-meta">{formatFecha(n.creadoEn)}</div>
                </div>
                {!n.vista ? (
                  <Badge color="accent">NUEVO</Badge>
                ) : (
                  <Badge color="default">VISTO</Badge>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
