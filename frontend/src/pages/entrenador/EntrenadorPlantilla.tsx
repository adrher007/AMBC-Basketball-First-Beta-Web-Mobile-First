import { useEffect, useState } from 'react';
import './../admin/Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import Input from '../../components/Input';
import { LoadingState, EmptyState, ErrorState } from '../../components/States';
import { createRoster, getRoster, deleteRoster } from '../../lib/roster';
import { getMiEquipo } from '../../lib/staff';
import type { Posicion, Roster } from '../../lib/types';

const POSICIONES: Posicion[] = ['BASE', 'ESCOLTA', 'ALERO', 'ALA_PIVOT', 'PIVOT'];

export default function EntrenadorPlantilla() {
  const [roster, setRoster] = useState<Roster[]>([]);
  const [miEquipo, setMiEquipo] = useState<{ teamId: string; seasonId: string; teamNombre: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [nombre, setNombre] = useState('');
  const [apellido, setApellido] = useState('');
  const [posicion, setPosicion] = useState<Posicion>('BASE');
  const [numero, setNumero] = useState('');
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState('');
  const [sinEquipo, setSinEquipo] = useState(false);

  useEffect(() => {
    getMiEquipo()
      .then((me) => {
        if (!me || !me.teamId) {
          setSinEquipo(true);
          setLoading(false);
          return;
        }
        setMiEquipo(me);
      })
      .catch((err) => {
        if (err?.response?.status === 404) {
          setSinEquipo(true);
        } else {
          setError(true);
        }
        setLoading(false);
      });
  }, []);

  function load() {
    if (!miEquipo) return;
    setLoading(true);
    setError(false);
    getRoster(miEquipo.teamId, miEquipo.seasonId)
      .then(setRoster)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    if (miEquipo) load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [miEquipo]);

  async function handleAdd() {
    if (!nombre.trim() || !apellido.trim() || !numero) {
      setMsg('Completa nombre, apellido y número.');
      return;
    }
    setSaving(true);
    setMsg('');
    try {
      await createRoster({
        nombre,
        apellido,
        posicion,
        numero: parseInt(numero, 10),
      });
      setMsg('✓ Jugador agregado correctamente');
      setNombre('');
      setApellido('');
      setNumero('');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos agregar el jugador.');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(r: Roster) {
    if (!confirm(`¿Eliminar a ${r.nombre} ${r.apellido} de la plantilla?`)) return;
    setMsg('');
    try {
      await deleteRoster(r.playerId);
      setMsg('✓ Jugador eliminado correctamente');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No se pudo eliminar el jugador.');
    }
  }

  return (
    <div className="page">
      <h1>Plantilla</h1>
      {miEquipo && <p className="page-sub">{miEquipo.teamNombre}</p>}

      {msg && <div className="msg">{msg}</div>}

      {sinEquipo && (
        <Card>
          <div className="msg">
            No tienes un equipo asignado. Primero el Administrador debe
            asignarte un equipo y temporada en el módulo de Entrenadores antes
            de poder registrar tu plantilla o jugadores.
          </div>
        </Card>
      )}

      {!sinEquipo && (
      <Card>
        <h2>Agregar jugador</h2>
        <div className="form-grid">
          <Input label="Nombre" value={nombre} onChange={setNombre} placeholder="Nombre" />
          <Input label="Apellido" value={apellido} onChange={setApellido} placeholder="Apellido" />
          <label className="field">
            <span className="field-label">Posición</span>
            <select
              className="input"
              value={posicion}
              onChange={(e) => setPosicion(e.target.value as Posicion)}
            >
              {POSICIONES.map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </select>
          </label>
          <Input label="Número" type="number" value={numero} onChange={setNumero} placeholder="Ej: 7" />
        </div>
        <div className="form-actions">
          <Button onClick={handleAdd} loading={saving}>
            Agregar
          </Button>
        </div>
      </Card>
      )}

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="la plantilla" onRetry={load} />}
      {!loading && !error && roster.length === 0 && <EmptyState label="jugadores" />}
      {!loading && !error && roster.length > 0 && (
        <div className="list">
          {roster.map((r) => (
            <Card key={r.playerId}>
              <div className="row">
                <div className="row-main">
                  <div className="row-title">
                    #{r.numero} · {r.nombre} {r.apellido}
                  </div>
                  <div className="row-sub">{r.posicion}</div>
                </div>
                <div className="row-actions">
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
