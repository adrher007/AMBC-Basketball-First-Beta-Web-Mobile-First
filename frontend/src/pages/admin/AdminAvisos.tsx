import { useState } from 'react';
import './Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import Input from '../../components/Input';
import { crearNotificacion } from '../../lib/notificaciones';

export default function AdminAvisos() {
  const [mensaje, setMensaje] = useState('');
  const [destinatario, setDestinatario] = useState('ALL');
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState('');

  async function handleCreate() {
    if (!mensaje.trim()) {
      setMsg('Escribe el mensaje.');
      return;
    }
    setSaving(true);
    setMsg('');
    try {
      await crearNotificacion(mensaje, destinatario);
      setMsg('✓ Aviso publicado correctamente');
      setMensaje('');
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos publicar el aviso.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="page">
      <h1>Avisos</h1>

      {msg && <div className="msg">{msg}</div>}

      <Card>
        <h2>Nuevo aviso</h2>
        <div className="form-grid">
          <Input
            label="Mensaje"
            value={mensaje}
            onChange={setMensaje}
            placeholder="Escribe el aviso…"
          />
          <label className="field">
            <span className="field-label">Destinatario</span>
            <select
              className="input"
              value={destinatario}
              onChange={(e) => setDestinatario(e.target.value)}
            >
              <option value="ALL">Todos</option>
              <option value="COACH">Entrenadores</option>
              <option value="ARBITRO">Árbitros</option>
              <option value="FAN">Fans</option>
            </select>
          </label>
        </div>
        <p className="page-sub">
          Los usuarios verán el aviso al abrir la app. Marcan "visto" de forma individual.
        </p>
        <div className="form-actions">
          <Button onClick={handleCreate} loading={saving}>Publicar</Button>
        </div>
      </Card>
    </div>
  );
}
