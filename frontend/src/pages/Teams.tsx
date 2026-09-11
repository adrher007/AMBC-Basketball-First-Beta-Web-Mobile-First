import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import './Teams.module.css';
import Card from '../components/Card';
import { LoadingState, EmptyState, ErrorState } from '../components/States';
import { getTeams } from '../lib/catalog';
import type { Team } from '../lib/types';

export default function Teams() {
  const [teams, setTeams] = useState<Team[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const navigate = useNavigate();

  function load() {
    setLoading(true);
    setError(false);
    getTeams()
      .then(setTeams)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  return (
    <div className="page">
      <h1>Equipos</h1>
      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los equipos" onRetry={load} />}
      {!loading && !error && teams.length === 0 && (
        <EmptyState label="equipos" />
      )}
      {!loading && !error && teams.length > 0 && (
        <div className="team-grid">
          {teams.map((t) => (
            <Card key={t.id} className="team-card" onClick={() => navigate(`/equipos/${t.id}`)}>
              <div className="team-logo">
                {t.logoUrl ? (
                  <img src={`http://localhost:5083${t.logoUrl}`} alt={t.nombre} />
                ) : (
                  <span>{t.nombreCorto}</span>
                )}
              </div>
              <div className="team-name">{t.nombre}</div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
