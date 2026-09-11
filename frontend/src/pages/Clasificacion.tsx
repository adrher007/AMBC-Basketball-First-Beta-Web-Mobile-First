import { useEffect, useState } from 'react';
import './Clasificacion.module.css';
import Card from '../components/Card';
import { LoadingState, EmptyState, ErrorState } from '../components/States';
import { getSeasons } from '../lib/catalog';
import { getStandings } from '../lib/standings';
import type { Season, StandingRow } from '../lib/types';

export default function Clasificacion() {
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [seasonId, setSeasonId] = useState('');
  const [rows, setRows] = useState<StandingRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    getSeasons().then((list) => {
      setSeasons(list);
      const active = list.find((s) => s.estado === 'ACTIVE');
      if (active) setSeasonId(active.id);
    });
  }, []);

  function load(sid?: string) {
    if (!sid) { setRows([]); setLoading(false); return; }
    setLoading(true);
    setError(false);
    getStandings(sid)
      .then(setRows)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    load(seasonId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seasonId]);

  return (
    <div className="page">
      <div className="head-row">
        <h1>Clasificación</h1>
        <select
          className="filter"
          value={seasonId}
          onChange={(e) => setSeasonId(e.target.value)}
        >
          <option value="">Seleccionar temporada</option>
          {seasons.map((s) => (
            <option key={s.id} value={s.id}>
              {s.nombre}
            </option>
          ))}
        </select>
      </div>

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="la clasificación" onRetry={() => load(seasonId)} />}
      {!loading && !error && rows.length === 0 && <EmptyState label="clasificación" />}

      {!loading && !error && rows.length > 0 && (
        <Card className="table-card">
          <table className="table">
            <thead>
              <tr>
                <th>#</th>
                <th>Equipo</th>
                <th>PJ</th>
                <th>V</th>
                <th>D</th>
                <th>Pts</th>
                <th>PF</th>
                <th>PC</th>
                <th>Diff</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.posicion} className={r.posicion <= 8 ? '' : 'relegated'}>
                  <td>{r.posicion}</td>
                  <td className="team-col">
                    <span className="team-corto">{r.nombreCorto}</span>
                    <span className="team-full">{r.equipo}</span>
                  </td>
                  <td>{r.pj}</td>
                  <td>{r.victorias}</td>
                  <td>{r.derrotas}</td>
                  <td className="pts">{r.pts}</td>
                  <td>{r.pf}</td>
                  <td>{r.pc}</td>
                  <td className={r.diff > 0 ? 'pos' : r.diff < 0 ? 'neg' : ''}>
                    {r.diff > 0 ? `+${r.diff}` : r.diff}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}
