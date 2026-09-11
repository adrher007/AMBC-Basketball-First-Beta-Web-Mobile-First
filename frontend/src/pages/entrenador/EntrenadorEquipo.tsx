import { useEffect, useState } from 'react';
import './../admin/Admin.module.css';
import Card from '../../components/Card';
import Badge from '../../components/Badge';
import { LoadingState, EmptyState, ErrorState } from '../../components/States';
import { getMiEquipo, subirLogoEquipo } from '../../lib/staff';
import { getRoster } from '../../lib/roster';
import type { MiEquipo, Roster } from '../../lib/types';

export default function EntrenadorEquipo() {
  const [miEquipo, setMiEquipo] = useState<MiEquipo | null>(null);
  const [roster, setRoster] = useState<Roster[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [sinEquipo, setSinEquipo] = useState(false);
  const [msg, setMsg] = useState('');
  const [subiendo, setSubiendo] = useState(false);

  function load() {
    setLoading(true);
    setError(false);
    setSinEquipo(false);
    getMiEquipo()
      .then((me) => {
        setMiEquipo(me);
        return getRoster(me.teamId, me.seasonId);
      })
      .then(setRoster)
      .catch((err) => {
        const status = err?.response?.status;
        if (status === 404) setSinEquipo(true);
        else setError(true);
      })
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  async function handleLogo(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setSubiendo(true);
    setMsg('');
    try {
      const res = await subirLogoEquipo(file);
      setMsg('✓ Logo actualizado correctamente');
      // Actualizar el logo en el estado
      setMiEquipo((prev) => (prev ? { ...prev, logoUrl: res.logoUrl } : prev));
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos subir el logo.');
    } finally {
      setSubiendo(false);
      e.target.value = '';
    }
  }

  return (
    <div className="page">
      <h1>Mi equipo</h1>

      {msg && <div className="msg">{msg}</div>}

      {loading && <LoadingState />}
      {!loading && error && (
        <ErrorState label="tu equipo" onRetry={load} />
      )}

      {!loading && sinEquipo && (
        <Card>
          <div className="msg">
            No tienes un equipo asignado. Primero el Administrador debe
            asignarte un equipo y temporada en el módulo de Entrenadores antes
            de poder registrar tu plantilla o jugadores.
          </div>
        </Card>
      )}

      {!loading && !error && !sinEquipo && miEquipo && (
        <>
          <Card>
            <div className="row">
              <div className="team-logo-box">
                {miEquipo.logoUrl ? (
                  <img
                    src={`http://localhost:5083${miEquipo.logoUrl}`}
                    alt={miEquipo.teamNombre}
                    className="team-logo-img"
                  />
                ) : (
                  <div className="team-logo-placeholder">
                    {miEquipo.teamNombreCorto}
                  </div>
                )}
              </div>
              <div className="row-main">
                <div className="row-title">{miEquipo.teamNombre}</div>
                <div className="row-sub">{miEquipo.seasonNombre}</div>
              </div>
              <Badge color="accent">{miEquipo.teamNombreCorto}</Badge>
            </div>

            <div className="logo-upload">
              <label className="logo-btn">
                {subiendo ? 'Subiendo…' : 'Subir logo'}
                <input
                  type="file"
                  accept=".png,.jpg,.jpeg,.svg,image/png,image/jpeg,image/svg+xml"
                  onChange={handleLogo}
                  disabled={subiendo}
                />
              </label>
              <span className="logo-hint">PNG, JPG o SVG · máx 5 MB</span>
            </div>
          </Card>

          <h2>Plantilla</h2>
          {roster.length === 0 && <EmptyState label="jugadores en tu plantilla" />}
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
                </div>
              </Card>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
