import { useEffect, useState } from 'react';
import './Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import { LoadingState, EmptyState, ErrorState } from '../../components/States';
import { getSeasons, getTeams } from '../../lib/catalog';
import { getPlayoffs, iniciarPlayoffs } from '../../lib/playoffs';
import type { PlayoffSerie, Season, Team } from '../../lib/types';

export default function AdminPlayoffs() {
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [seasonId, setSeasonId] = useState('');
  const [series, setSeries] = useState<PlayoffSerie[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [msg, setMsg] = useState('');
  const [starting, setStarting] = useState(false);

  useEffect(() => {
    getTeams().then(setTeams).catch(() => {});
    getSeasons().then((list) => {
      setSeasons(list);
      const active = list.find((s) => s.estado === 'ACTIVE');
      if (active) setSeasonId(active.id);
    });
  }, []);

  function load(sid?: string) {
    if (!sid) { setSeries([]); setLoading(false); return; }
    setLoading(true);
    setError(false);
    getPlayoffs(sid)
      .then(setSeries)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    load(seasonId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seasonId]);

  async function handleIniciar() {
    if (!seasonId) return;
    setStarting(true);
    setMsg('');
    try {
      const res = await iniciarPlayoffs(seasonId);
      setMsg(`✓ ${res.mensaje}`);
      load(seasonId);
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos iniciar los playoffs.');
    } finally {
      setStarting(false);
    }
  }

  const teamName = (id: string) => teams.find((t) => t.id === id)?.nombre ?? '';

  return (
    <div className="page">
      <div className="head-row">
        <h1>Playoffs</h1>
        <select className="filter" value={seasonId} onChange={(e) => setSeasonId(e.target.value)}>
          <option value="">Seleccionar temporada</option>
          {seasons.map((s) => (
            <option key={s.id} value={s.id}>{s.nombre}</option>
          ))}
        </select>
        <Button onClick={handleIniciar} loading={starting}>
          Iniciar playoffs
        </Button>
      </div>

      {msg && <div className="msg">{msg}</div>}

      <Card>
        <h2>Iniciar playoffs</h2>
        <p className="page-sub">
          Requiere la fase regular completa (72 partidos finalizados) y 8 equipos
          clasificados. Crea los QF (1v8, 2v7, 3v6, 4v5) al mejor de 3.
        </p>
      </Card>

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los playoffs" onRetry={() => load(seasonId)} />}
      {!loading && !error && series.length === 0 && (
        <EmptyState label="series de playoffs" />
      )}

      {!loading && !error && series.length > 0 && (
        <div className="list">
          {series.map((s) => (
            <Card key={s.id}>
              <div className="row">
                <div className="row-main">
                  <div className="row-title">{s.fase}</div>
                  <div className="row-sub">
                    {teamName(s.teamAId)} {s.winsA} — {s.winsB} {teamName(s.teamBId)}
                    {' '}· al mejor de {s.bestOf}
                  </div>
                  <div className="row-sub">
                    {s.estado}
                    {s.ganadorId ? ` · ganador: ${teamName(s.ganadorId)}` : ''}
                  </div>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
