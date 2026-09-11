import { useEffect, useState } from 'react';
import './Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import Input from '../../components/Input';
import { LoadingState, EmptyState, ErrorState } from '../../components/States';
import { getTeams, createTeam, updateTeam, deleteTeam } from '../../lib/catalog';
import type { Team } from '../../lib/types';

export default function AdminEquipos() {
  const [teams, setTeams] = useState<Team[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Team | null>(null);
  const [nombre, setNombre] = useState('');
  const [nombreCorto, setNombreCorto] = useState('');
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState('');

  function load() {
    setLoading(true);
    setError(false);
    getTeams()
      .then(setTeams)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  function openCreate() {
    setEditing(null);
    setNombre('');
    setNombreCorto('');
    setMsg('');
    setShowForm(true);
  }

  function openEdit(t: Team) {
    setEditing(t);
    setNombre(t.nombre);
    setNombreCorto(t.nombreCorto);
    setMsg('');
    setShowForm(true);
  }

  async function handleSave() {
    setSaving(true);
    setMsg('');
    try {
      if (editing) {
        await updateTeam(editing.id, { nombre, nombreCorto });
        setMsg('✓ Equipo actualizado correctamente');
      } else {
        await createTeam({ nombre, nombreCorto });
        setMsg('✓ Equipo registrado correctamente');
      }
      setShowForm(false);
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos guardar el equipo');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(t: Team) {
    if (!confirm(`¿Eliminar el equipo ${t.nombre}?`)) return;
    setMsg('');
    try {
      await deleteTeam(t.id);
      setMsg('✓ Equipo eliminado correctamente');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No se pudo eliminar el equipo');
    }
  }

  return (
    <div className="page">
      <div className="head-row">
        <h1>Equipos</h1>
        <Button onClick={openCreate}>Nuevo equipo</Button>
      </div>

      {msg && <div className="msg">{msg}</div>}

      {showForm && (
        <Card>
          <h2>{editing ? 'Editar equipo' : 'Nuevo equipo'}</h2>
          <div className="form-grid">
            <Input label="Nombre completo" value={nombre} onChange={setNombre} placeholder="Ej: La Salle" />
            <Input label="Nombre corto / sigla" value={nombreCorto} onChange={setNombreCorto} placeholder="Ej: LAS" />
          </div>
          <div className="form-actions">
            <Button variant="secondary" onClick={() => setShowForm(false)}>
              Cancelar
            </Button>
            <Button onClick={handleSave} loading={saving}>
              Guardar
            </Button>
          </div>
        </Card>
      )}

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los equipos" onRetry={load} />}
      {!loading && !error && teams.length === 0 && <EmptyState label="equipos" />}
      {!loading && !error && teams.length > 0 && (
        <div className="list">
          {teams.map((t) => (
            <Card key={t.id}>
              <div className="row">
                <div className="row-main">
                  <div className="row-title">{t.nombre}</div>
                  <div className="row-sub">{t.nombreCorto}</div>
                </div>
                <div className="row-actions">
                  <Button variant="secondary" onClick={() => openEdit(t)}>
                    Editar
                  </Button>
                  <Button variant="danger" onClick={() => handleDelete(t)}>
                    Eliminar
                  </Button>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
