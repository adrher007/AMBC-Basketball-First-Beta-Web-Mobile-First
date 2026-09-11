import { useEffect, useState } from 'react';
import './Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import Input from '../../components/Input';
import { LoadingState, EmptyState, ErrorState } from '../../components/States';
import { getCoaches, createCoach, deleteCoach, resetCoachPassword } from '../../lib/staff';
import { getTeams, getSeasons } from '../../lib/catalog';
import type { Coach, Season, Team } from '../../lib/types';

export default function AdminEntrenadores() {
  const [coaches, setCoaches] = useState<Coach[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [nombre, setNombre] = useState('');
  const [teamId, setTeamId] = useState('');
  const [seasonId, setSeasonId] = useState('');
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState('');

  function load() {
    setLoading(true);
    setError(false);
    getCoaches()
      .then(setCoaches)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    load();
    getTeams().then(setTeams).catch(() => {});
    getSeasons().then((list) => {
      setSeasons(list);
      const active = list.find((s) => s.estado === 'ACTIVE');
      if (active) setSeasonId(active.id);
    });
  }, []);

  async function handleCreate() {
    if (!nombre.trim() || !teamId || !seasonId) {
      setMsg('Completa nombre, equipo y temporada.');
      return;
    }
    setSaving(true);
    setMsg('');
    try {
      const c = await createCoach({ nombreCompleto: nombre, teamId, seasonId });
      setMsg(`✓ Entrenador creado: ${c.username} (password: coach123)`);
      setNombre('');
      setTeamId('');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos crear el entrenador.');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(c: Coach) {
    if (!confirm(`¿Eliminar al entrenador ${c.nombreCompleto}?`)) return;
    setMsg('');
    try {
      await deleteCoach(c.id);
      setMsg('✓ Entrenador eliminado correctamente');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No se pudo eliminar el entrenador.');
    }
  }

  async function handleResetPassword(c: Coach) {
    const nueva = prompt(`Nueva contraseña para ${c.nombreCompleto} (mínimo 4 caracteres):`);
    if (!nueva || nueva.length < 4) {
      setMsg('Contraseña inválida (mínimo 4 caracteres).');
      return;
    }
    setMsg('');
    try {
      await resetCoachPassword(c.id, nueva);
      setMsg(`✓ Contraseña de ${c.username} actualizada`);
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No se pudo actualizar la contraseña.');
    }
  }

  return (
    <div className="page">
      <h1>Entrenadores</h1>

      {msg && <div className="msg">{msg}</div>}

      <Card>
        <h2>Nuevo entrenador</h2>
        <div className="form-grid">
          <Input label="Nombre completo" value={nombre} onChange={setNombre} placeholder="Ej: Carlos Perez" />
          <label className="field">
            <span className="field-label">Equipo</span>
            <select className="input" value={teamId} onChange={(e) => setTeamId(e.target.value)}>
              <option value="">Seleccionar…</option>
              {teams.map((t) => (
                <option key={t.id} value={t.id}>{t.nombre}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span className="field-label">Temporada</span>
            <select className="input" value={seasonId} onChange={(e) => setSeasonId(e.target.value)}>
              <option value="">Seleccionar…</option>
              {seasons.map((s) => (
                <option key={s.id} value={s.id}>{s.nombre} ({s.estado})</option>
              ))}
            </select>
          </label>
        </div>
        <p className="page-sub">
          Usuario de login: nombre completo en minúsculas sin espacios. Contraseña: coach123.
        </p>
        <div className="form-actions">
          <Button onClick={handleCreate} loading={saving}>Crear</Button>
        </div>
      </Card>

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los entrenadores" onRetry={load} />}
      {!loading && !error && coaches.length === 0 && <EmptyState label="entrenadores" />}
      {!loading && !error && coaches.length > 0 && (
        <div className="list">
          {coaches.map((c) => (
            <Card key={c.id}>
              <div className="row">
                <div className="row-main">
                  <div className="row-title">{c.nombreCompleto}</div>
                  <div className="row-sub">{c.teamNombre} · {c.username}</div>
                </div>
                <div className="row-actions">
                  <Button variant="secondary" onClick={() => handleResetPassword(c)}>
                    Reset contraseña
                  </Button>
                  <Button variant="danger" onClick={() => handleDelete(c)}>
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
