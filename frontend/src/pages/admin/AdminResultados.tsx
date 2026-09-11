import { useEffect, useState } from 'react';
import './Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import Input from '../../components/Input';
import DateInput from '../../components/DateInput';
import Badge from '../../components/Badge';
import { LoadingState, EmptyState, ErrorState } from '../../components/States';
import { getGames, rescheduleGame, setGameResult } from '../../lib/roster';
import { getTeams } from '../../lib/catalog';
import type { Game, Team } from '../../lib/types';

export default function AdminResultados() {
  const [games, setGames] = useState<Game[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [msg, setMsg] = useState('');

  // Estado para el partido en edición
  const [editingId, setEditingId] = useState<string | null>(null);
  const [local, setLocal] = useState('');
  const [visitante, setVisitante] = useState('');
  const [saving, setSaving] = useState(false);

  // Estado para reprogramar
  const [reschedId, setReschedId] = useState<string | null>(null);
  const [fecha, setFecha] = useState('');
  const [hora, setHora] = useState('');

  function load() {
    setLoading(true);
    setError(false);
    getTeams().then(setTeams).catch(() => {});
    getGames()
      .then(setGames)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  const teamName = (id: string) =>
    teams.find((t) => t.id === id)?.nombreCorto ?? '—';

  async function handleResult(g: Game) {
    if (!local || !visitante) {
      setMsg('Ingresa ambos marcadores.');
      return;
    }
    setSaving(true);
    setMsg('');
    try {
      await setGameResult(g.id, parseInt(local, 10), parseInt(visitante, 10));
      setMsg(`✓ Resultado registrado: ${teamName(g.homeTeamId)} ${local} - ${visitante} ${teamName(g.awayTeamId)}`);
      setEditingId(null);
      setLocal('');
      setVisitante('');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos registrar el resultado.');
    } finally {
      setSaving(false);
    }
  }

  async function handleReschedule(g: Game) {
    if (!fecha || !hora) {
      setMsg('Ingresa fecha y hora.');
      return;
    }
    setSaving(true);
    setMsg('');
    try {
      await rescheduleGame(g.id, fecha, hora);
      setMsg('✓ Partido reprogramado correctamente');
      setReschedId(null);
      setFecha('');
      setHora('');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos reprogramar.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="page">
      <h1>Resultados</h1>

      {msg && <div className="msg">{msg}</div>}

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los partidos" onRetry={load} />}
      {!loading && !error && games.length === 0 && <EmptyState label="partidos" />}

      {!loading && !error && games.length > 0 && (
        <div className="list">
          {games.map((g) => (
            <Card key={g.id}>
              <div className="row">
                <div className="row-main">
                  <div className="row-title">
                    {teamName(g.homeTeamId)} vs {teamName(g.awayTeamId)}
                  </div>
                  <div className="row-sub">
                    {g.fecha} · {g.hora}
                  </div>
                </div>

                {g.estado === 'FINISHED' ? (
                  <Badge color="success">
                    {g.marcadorLocal} - {g.marcadorVisitante}
                  </Badge>
                ) : (
                  <Badge color="accent">PENDIENTE</Badge>
                )}

                <div className="row-actions">
                  {g.estado === 'SCHEDULED' && (
                    <>
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setReschedId(reschedId === g.id ? null : g.id);
                          setEditingId(null);
                          setFecha(g.fecha);
                          setHora(g.hora);
                        }}
                      >
                        Reprogramar
                      </Button>
                      <Button
                        onClick={() => {
                          setEditingId(editingId === g.id ? null : g.id);
                          setReschedId(null);
                          setLocal('');
                          setVisitante('');
                        }}
                      >
                        Resultado
                      </Button>
                    </>
                  )}
                </div>
              </div>

              {editingId === g.id && (
                <div className="inline-form">
                  <Input label="Local" type="number" value={local} onChange={setLocal} placeholder="0" />
                  <Input label="Visitante" type="number" value={visitante} onChange={setVisitante} placeholder="0" />
                  <Button onClick={() => handleResult(g)} loading={saving}>
                    Guardar
                  </Button>
                </div>
              )}

              {reschedId === g.id && (
                <div className="inline-form">
                  <DateInput label="Fecha" value={fecha} onChange={setFecha} />
                  <Input label="Hora" value={hora} onChange={setHora} placeholder="HH:mm" />
                  <Button onClick={() => handleReschedule(g)} loading={saving}>
                    Guardar
                  </Button>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
