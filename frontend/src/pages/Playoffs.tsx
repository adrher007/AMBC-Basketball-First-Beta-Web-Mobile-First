import { useEffect, useState } from 'react';
import './Playoffs.module.css';
import Card from '../components/Card';
import { LoadingState, EmptyState, ErrorState } from '../components/States';
import { getSeasons, getTeams } from '../lib/catalog';
import { getPlayoffs } from '../lib/playoffs';
import type { PlayoffSerie, Season, Team } from '../lib/types';

const FASE_LABEL: Record<string, string> = {
  QF: 'Cuartos de final',
  SF: 'Semifinal',
  FINAL: 'Final',
  TERCER_LUGAR: 'Tercer lugar',
};

export default function Playoffs() {
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [seasonId, setSeasonId] = useState('');
  const [series, setSeries] = useState<PlayoffSerie[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

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

  const teamName = (id: string) =>
    teams.find((t) => t.id === id)?.nombre ?? '';

  const order = ['QF', 'SF', 'FINAL', 'TERCER_LUGAR'];

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
      </div>

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los playoffs" onRetry={() => load(seasonId)} />}
      {!loading && !error && series.length === 0 && <EmptyState label="playoffs" />}

      {!loading && !error && series.length > 0 &&
        order.map((fase) => {
          const faseSeries = series.filter((s) => s.fase === fase);
          if (faseSeries.length === 0) return null;
          return (
            <div key={fase} className="bracket">
              <h2 className="bracket-title">{FASE_LABEL[fase] ?? fase}</h2>
              {faseSeries.map((s) => (
                <Card key={s.id}>
                  <div className="serie">
                    <div className={`serie-team ${s.ganadorId === s.teamAId ? 'winner' : ''}`}>
                      <span className="serie-name">{teamName(s.teamAId)}</span>
                      <span className="serie-wins">{s.winsA}</span>
                    </div>
                    <div className={`serie-team ${s.ganadorId === s.teamBId ? 'winner' : ''}`}>
                      <span className="serie-name">{teamName(s.teamBId)}</span>
                      <span className="serie-wins">{s.winsB}</span>
                    </div>
                    <div className="serie-meta">
                      Al mejor de {s.bestOf} · {s.estado}
                    </div>
                  </div>
                </Card>
              ))}
            </div>
          );
        })}
    </div>
  );
}
