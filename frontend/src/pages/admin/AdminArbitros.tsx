import { useEffect, useState } from 'react';
import './Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import Input from '../../components/Input';
import { LoadingState, EmptyState, ErrorState } from '../../components/States';
import { getReferees, createReferee, deleteReferee, resetRefereePassword } from '../../lib/staff';
import type { Referee } from '../../lib/types';

export default function AdminArbitros() {
  const [referees, setReferees] = useState<Referee[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [nombre, setNombre] = useState('');
  const [password, setPassword] = useState('');
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState('');

  function load() {
    setLoading(true);
    setError(false);
    getReferees()
      .then(setReferees)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  async function handleCreate() {
    if (!nombre.trim() || !password) {
      setMsg('Completa nombre y contraseña.');
      return;
    }
    setSaving(true);
    setMsg('');
    try {
      const r = await createReferee({ nombreCompleto: nombre, password });
      setMsg(`✓ Árbitro creado: ${r.username}`);
      setNombre('');
      setPassword('');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos crear el árbitro.');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(r: Referee) {
    if (!confirm(`¿Eliminar al árbitro ${r.nombreCompleto}?`)) return;
    setMsg('');
    try {
      await deleteReferee(r.id);
      setMsg('✓ Árbitro eliminado correctamente');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No se pudo eliminar el árbitro.');
    }
  }

  async function handleResetPassword(r: Referee) {
    const nueva = prompt(`Nueva contraseña para ${r.nombreCompleto} (mínimo 4 caracteres):`);
    if (!nueva || nueva.length < 4) {
      setMsg('Contraseña inválida (mínimo 4 caracteres).');
      return;
    }
    setMsg('');
    try {
      await resetRefereePassword(r.id, nueva);
      setMsg(`✓ Contraseña de ${r.username} actualizada`);
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No se pudo actualizar la contraseña.');
    }
  }

  return (
    <div className="page">
      <h1>Árbitros</h1>

      {msg && <div className="msg">{msg}</div>}

      <Card>
        <h2>Nuevo árbitro</h2>
        <div className="form-grid">
          <Input label="Nombre completo" value={nombre} onChange={setNombre} placeholder="Ej: Juan Lopez" />
          <Input label="Contraseña" type="password" value={password} onChange={setPassword} placeholder="Personalizada" />
        </div>
        <p className="page-sub">
          Usuario de login: nombre completo en minúsculas sin espacios. La contraseña la defines tú.
        </p>
        <div className="form-actions">
          <Button onClick={handleCreate} loading={saving}>Crear</Button>
        </div>
      </Card>

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los árbitros" onRetry={load} />}
      {!loading && !error && referees.length === 0 && <EmptyState label="árbitros" />}
      {!loading && !error && referees.length > 0 && (
        <div className="list">
          {referees.map((r) => (
            <Card key={r.id}>
              <div className="row">
                <div className="row-main">
                  <div className="row-title">{r.nombreCompleto}</div>
                  <div className="row-sub">{r.username}</div>
                </div>
                <div className="row-actions">
                  <Button variant="secondary" onClick={() => handleResetPassword(r)}>
                    Reset contraseña
                  </Button>
                  <Button variant="danger" onClick={() => handleDelete(r)}>
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
