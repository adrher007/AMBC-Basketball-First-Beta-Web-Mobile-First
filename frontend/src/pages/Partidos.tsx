import { useEffect, useState } from 'react';
import './Partidos.module.css';
import Card from '../components/Card';
import { LoadingState, EmptyState, ErrorState } from '../components/States';
import { getGames } from '../lib/roster';
import { getSeasons, getTeams } from '../lib/catalog';
import type { Game, Season, Team } from '../lib/types';

export default function Partidos() {
  const [games, setGames] = useState<Game[]>([]);
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [seasonId, setSeasonId] = useState('');
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
    setLoading(true);
    setError(false);
    getGames(sid || undefined)
      .then(setGames)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    load(seasonId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seasonId]);

  const teamById = (id: string) => teams.find((t) => t.id === id);

  // Agrupar por fecha (jornada)
  const byDate = new Map<string, Game[]>();
  for (const g of games) {
    const key = g.fecha;
    if (!byDate.has(key)) byDate.set(key, []);
    byDate.get(key)!.push(g);
  }

  function Escudo({ team }: { team?: Team }) {
    return (
      <div className="fx-shield">
        {team?.logoUrl ? (
          <img src={`http://localhost:5083${team.logoUrl}`} alt={team.nombre} />
        ) : (
          <span>{team?.nombreCorto ?? '?'}</span>
        )}
      </div>
    );
  }

  return (
    <div className="page">
      <div className="head-row">
        <h1>Partidos</h1>
        <select
          className="filter"
          value={seasonId}
          onChange={(e) => setSeasonId(e.target.value)}
        >
          <option value="">Todas las temporadas</option>
          {seasons.map((s) => (
            <option key={s.id} value={s.id}>
              {s.nombre}
            </option>
          ))}
        </select>
      </div>

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los partidos" onRetry={() => load(seasonId)} />}
      {!loading && !error && games.length === 0 && <EmptyState label="partidos" />}

      {!loading &&
        !error &&
        [...byDate.entries()].map(([fecha, dayGames]) => (
          <div key={fecha} className="day">
            <div className="day-head">
              <h3>{fecha}</h3>
              <span className="day-count">{dayGames.length} partidos</span>
            </div>
            {dayGames.map((g) => {
              const home = teamById(g.homeTeamId);
              const away = teamById(g.awayTeamId);
              const finalizado = g.estado === 'FINISHED';
              return (
                <Card key={g.id}>
                  <div className="fx">
                    <div className="fx-team">
                      <Escudo team={home} />
                      <span className="fx-name">{home?.nombre ?? '?'}</span>
                    </div>

                    <div className="fx-center">
                      <span className="fx-vs">VS</span>
                      {finalizado ? (
                        <span className="fx-score">
                          {g.marcadorLocal} - {g.marcadorVisitante}
                        </span>
                      ) : (
                        <span className="fx-time">{g.hora}</span>
                      )}
                    </div>

                    <div className="fx-team">
                      <Escudo team={away} />
                      <span className="fx-name">{away?.nombre ?? '?'}</span>
                    </div>
                  </div>
                  {finalizado && <div className="fx-final">FINALIZADO</div>}
                </Card>
              );
            })}
          </div>
        ))}
    </div>
  );
}
