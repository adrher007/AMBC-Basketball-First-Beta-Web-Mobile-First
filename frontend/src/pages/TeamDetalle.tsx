import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import './Teams.module.css';
import Card from '../components/Card';
import { LoadingState, ErrorState, EmptyState } from '../components/States';
import { getTeamDetalle } from '../lib/catalog';
import type { TeamDetalle } from '../lib/types';

export default function TeamDetallePage() {
  const { id } = useParams<{ id: string }>();
  const [team, setTeam] = useState<TeamDetalle | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  function load() {
    if (!id) return;
    setLoading(true);
    setError(false);
    getTeamDetalle(id)
      .then(setTeam)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, [id]);

  return (
    <div className="page">
      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="el equipo" onRetry={load} />}

      {!loading && !error && team && (
        <>
          <Card>
            <div className="team-header">
              <div className="team-logo">
                {team.logoUrl ? (
                  <img src={`http://localhost:5083${team.logoUrl}`} alt={team.nombre} />
                ) : (
                  <span>{team.nombreCorto}</span>
                )}
              </div>
              <div>
                <h1>{team.nombre}</h1>
                <p className="page-sub">{team.temporada ?? 'Sin temporada activa'}</p>
              </div>
            </div>
          </Card>

          <h2>Cuerpo técnico</h2>
          {team.cuerpoTecnico.length === 0 && <EmptyState label="cuerpo técnico" />}
          <div className="list">
            {team.cuerpoTecnico.map((ct, i) => (
              <Card key={i}>
                <div className="row">
                  <div className="row-main">
                    <div className="row-title">{ct.nombre}</div>
                    <div className="row-sub">{ct.rol}</div>
                  </div>
                </div>
              </Card>
            ))}
          </div>

          <h2>Plantilla ({team.plantilla.length})</h2>
          {team.plantilla.length === 0 && <EmptyState label="jugadores" />}
          <div className="list">
            {team.plantilla.map((p) => (
              <Card key={p.id}>
                <div className="row">
                  <div className="row-main">
                    <div className="row-title">
                      #{p.numero} · {p.nombre} {p.apellido}
                    </div>
                    <div className="row-sub">{p.posicion}</div>
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
