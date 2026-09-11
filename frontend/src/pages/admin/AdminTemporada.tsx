import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import './Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import Input from '../../components/Input';
import DateInput from '../../components/DateInput';
import Badge from '../../components/Badge';
import { LoadingState, EmptyState, ErrorState } from '../../components/States';
import {
  getSeasons,
  createSeason,
  updateSeason,
  activateSeason,
  finishSeason,
} from '../../lib/catalog';
import type { Season } from '../../lib/types';

const estadoColor = (e: string): 'default' | 'accent' | 'success' =>
  e === 'ACTIVE' ? 'success' : e === 'UPCOMING' ? 'accent' : 'default';

// Fecha de hoy en formato YYYY-MM-DD (hora local)
function hoyISO() {
  const d = new Date();
  const off = d.getTimezoneOffset();
  return new Date(d.getTime() - off * 60000).toISOString().slice(0, 10);
}

export default function AdminTemporada() {
  const navigate = useNavigate();
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Season | null>(null);
  const [nombre, setNombre] = useState('');
  const [fechaInicio, setFechaInicio] = useState('');
  const [fechaFin, setFechaFin] = useState('');
  const [estado, setEstado] = useState('UPCOMING');
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState('');
  // Prompt contextual post-creación
  const [creada, setCreada] = useState<Season | null>(null);

  function load() {
    setLoading(true);
    setError(false);
    getSeasons()
      .then(setSeasons)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  function openCreate() {
    setEditing(null);
    setNombre('');
    setFechaInicio('');
    setFechaFin('');
    setEstado('UPCOMING');
    setMsg('');
    setCreada(null);
    setShowForm(true);
  }

  function openEdit(s: Season) {
    setEditing(s);
    setNombre(s.nombre);
    setFechaInicio(s.fechaInicio);
    setFechaFin(s.fechaFin);
    setEstado(s.estado);
    setMsg('');
    setCreada(null);
    setShowForm(true);
  }

  async function handleSave() {
    setSaving(true);
    setMsg('');
    try {
      if (editing) {
        await updateSeason(editing.id, { nombre, fechaInicio, fechaFin });
        setMsg('✓ Temporada actualizada correctamente');
        setShowForm(false);
      } else {
        const nueva = await createSeason({ nombre, fechaInicio, fechaFin, estado });
        setMsg('✓ Temporada creada exitosamente');
        setShowForm(false);
        setCreada(nueva);
      }
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos guardar la temporada');
    } finally {
      setSaving(false);
    }
  }

  async function toggleSeason(s: Season) {
    setMsg('');
    try {
      if (s.estado === 'ACTIVE') await finishSeason(s.id);
      else await activateSeason(s.id);
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'Error al cambiar el estado');
    }
  }

  return (
    <div className="page">
      <div className="head-row">
        <h1>Temporadas</h1>
        <Button onClick={openCreate}>Nueva temporada</Button>
      </div>

      {msg && <div className="msg">{msg}</div>}

      {/* Prompt contextual: programar partido de hoy */}
      {creada && (
        <Card>
          <div className="prompt-programar">
            <div className="prompt-text">
              <strong>¿Desea programar un partido para la fecha de hoy?</strong>
            </div>
            <div className="row-actions">
              <Button
                onClick={() =>
                  navigate(
                    `/admin/partidos/programar?fecha=${hoyISO()}&seasonId=${creada.id}`,
                  )
                }
              >
                Programar partido
              </Button>
              <Button variant="secondary" onClick={() => setCreada(null)}>
                Ahora no
              </Button>
            </div>
          </div>
        </Card>
      )}

      {showForm && (
        <Card>
          <h2>{editing ? 'Editar temporada' : 'Nueva temporada'}</h2>
          <div className="form-grid">
            <Input label="Nombre" value={nombre} onChange={setNombre} placeholder="Ej: Torneo Apertura 2026" />
            <DateInput label="Fecha de inicio" value={fechaInicio} onChange={setFechaInicio} />
            <DateInput label="Fecha de finalización" value={fechaFin} onChange={setFechaFin} />
            <label className="field">
              <span className="field-label">Estado</span>
              <select
                className="input"
                value={estado}
                onChange={(e) => setEstado(e.target.value)}
                disabled={!!editing}
              >
                <option value="UPCOMING">Próxima (UPCOMING)</option>
                <option value="ACTIVE">Activa (ACTIVE)</option>
                <option value="FINISHED">Finalizada (FINISHED)</option>
              </select>
            </label>
          </div>
          {editing && (
            <p className="page-sub">
              El estado se cambia con los botones Activar / Finalizar.
            </p>
          )}
          <div className="form-actions">
            <Button variant="secondary" onClick={() => setShowForm(false)}>
              Cancelar
            </Button>
            <Button onClick={handleSave} loading={saving}>
              Crear
            </Button>
          </div>
        </Card>
      )}

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="las temporadas" onRetry={load} />}
      {!loading && !error && seasons.length === 0 && (
        <EmptyState label="temporadas" />
      )}
      {!loading && !error && seasons.length > 0 && (
        <div className="list">
          {seasons.map((s) => (
            <Card key={s.id}>
              <div className="row">
                <div className="row-main">
                  <div className="row-title">{s.nombre}</div>
                  <div className="row-sub">
                    {s.fechaInicio} → {s.fechaFin}
                  </div>
                </div>
                <Badge color={estadoColor(s.estado)}>{s.estado}</Badge>
                <div className="row-actions">
                  {s.estado !== 'FINISHED' && (
                    <Button variant="secondary" onClick={() => openEdit(s)}>
                      Editar
                    </Button>
                  )}
                  {s.estado === 'UPCOMING' && (
                    <Button onClick={() => toggleSeason(s)}>Activar</Button>
                  )}
                  {s.estado === 'ACTIVE' && (
                    <Button variant="danger" onClick={() => toggleSeason(s)}>
                      Finalizar
                    </Button>
                  )}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
