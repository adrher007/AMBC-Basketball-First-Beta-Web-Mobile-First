import { useEffect, useState } from 'react';
import './Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import { LoadingState } from '../../components/States';
import { getSeasons, getTeams } from '../../lib/catalog';
import { getReferees } from '../../lib/staff';
import {
  getSemanas,
  getPropuestaSemana,
  confirmarSemana,
  type SemanaInfo,
  type PartidoSemana,
} from '../../lib/programacion';
import type { Season, Team, Referee } from '../../lib/types';

interface FilaPartido {
  fecha: string;
  hora: string;
  homeTeamId: string;
  awayTeamId: string;
  local: string;
  visitante: string;
  arbitroIds: string[];
}

export default function AdminProgramarSemana() {
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [referees, setReferees] = useState<Referee[]>([]);

  const [seasonId, setSeasonId] = useState('');
  const [semanas, setSemanas] = useState<SemanaInfo[]>([]);
  const [semana, setSemana] = useState<number | ''>('');
  const [rango, setRango] = useState<{ inicio: string; fin: string } | null>(null);
  const [rondasPendientes, setRondasPendientes] = useState<number | null>(null);

  const [filas, setFilas] = useState<FilaPartido[]>([]);
  const [cargando, setCargando] = useState(false);
  const [confirmando, setConfirmando] = useState(false);
  const [msg, setMsg] = useState('');

  useEffect(() => {
    getSeasons().then(setSeasons).catch(() => {});
    getTeams().then(setTeams).catch(() => {});
    getReferees().then(setReferees).catch(() => {});
  }, []);

  // Al cambiar temporada: cargar semanas y posicionarse en la primera no programada
  useEffect(() => {
    if (!seasonId) {
      setSemanas([]);
      setSemana('');
      setFilas([]);
      return;
    }
    getSemanas(seasonId)
      .then((d) => {
        setSemanas(d.semanas);
        const primeraLibre = d.semanas.find((s) => !s.programada);
        setSemana(primeraLibre?.semana ?? d.semanas[0]?.semana ?? '');
      })
      .catch(() => setSemanas([]));
  }, [seasonId]);

  // Al cambiar semana: pedir propuesta automática
  function cargarSemana(sid: string, sem: number) {
    setCargando(true);
    setMsg('');
    getPropuestaSemana(sid, sem)
      .then((d) => {
        setRango({ inicio: d.inicio, fin: d.fin });
        setRondasPendientes(d.rondasPendientes);
        setFilas(
          d.propuesta.map((p: PartidoSemana) => ({
            fecha: p.fecha,
            hora: p.hora,
            homeTeamId: p.homeTeamId,
            awayTeamId: p.awayTeamId,
            local: p.local,
            visitante: p.visitante,
            arbitroIds: ['', ''],
          })),
        );
      })
      .catch((err) => {
        setFilas([]);
        setMsg(err?.response?.data?.error ?? 'No pudimos generar la propuesta.');
      })
      .finally(() => setCargando(false));
  }

  useEffect(() => {
    if (seasonId && semana) cargarSemana(seasonId, Number(semana));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [semana]);

  function actualizarFila(idx: number, cambio: Partial<FilaPartido>) {
    setFilas((fs) => fs.map((f, i) => (i === idx ? { ...f, ...cambio } : f)));
  }

  function setArbitro(idx: number, pos: number, valor: string) {
    setFilas((fs) =>
      fs.map((f, i) => {
        if (i !== idx) return f;
        const arbs = [...f.arbitroIds];
        arbs[pos] = valor;
        return { ...f, arbitroIds: arbs };
      }),
    );
  }

  function agregarFila() {
    if (!rango) return;
    setFilas((fs) => [
      ...fs,
      {
        fecha: rango.inicio,
        hora: '19:00',
        homeTeamId: '',
        awayTeamId: '',
        local: '',
        visitante: '',
        arbitroIds: ['', ''],
      },
    ]);
  }

  function quitarFila(idx: number) {
    setFilas((fs) => fs.filter((_, i) => i !== idx));
  }

  async function confirmar() {
    if (!seasonId || !semana) return;
    if (filas.length === 0) {
      setMsg('La semana no tiene partidos.');
      return;
    }

    // Validación local (el backend vuelve a validar todo)
    for (const [i, f] of filas.entries()) {
      if (!f.homeTeamId || !f.awayTeamId) return setMsg(`Partido ${i + 1}: falta local o visitante.`);
      if (f.homeTeamId === f.awayTeamId) return setMsg(`Partido ${i + 1}: local y visitante son iguales.`);
      if (!f.fecha || !f.hora) return setMsg(`Partido ${i + 1}: falta fecha u hora.`);
      const arbs = f.arbitroIds.filter(Boolean);
      if (arbs.length < 2) return setMsg(`Partido ${i + 1}: se requieren al menos 2 árbitros.`);
      if (new Set(arbs).size !== arbs.length) return setMsg(`Partido ${i + 1}: árbitro repetido.`);
    }

    setConfirmando(true);
    setMsg('');
    try {
      const res = await confirmarSemana({
        seasonId,
        semana: Number(semana),
        partidos: filas.map((f) => ({
          fecha: f.fecha,
          hora: f.hora,
          homeTeamId: f.homeTeamId,
          awayTeamId: f.awayTeamId,
          arbitroIds: f.arbitroIds.filter(Boolean),
        })),
      });
      setMsg(`✓ Semana ${res.semana} confirmada: ${res.partidos} partidos creados y árbitros notificados.`);
      getSemanas(seasonId).then((d) => setSemanas(d.semanas)).catch(() => {});
      setFilas([]);
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos confirmar la semana.');
    } finally {
      setConfirmando(false);
    }
  }

  const selectorEquipos = (valor: string, onChange: (v: string) => void, conNombre = false) => (
    <select className="input" value={valor} onChange={(e) => onChange(e.target.value)}>
      <option value="">Seleccionar…</option>
      {teams.map((t) => (
        <option key={t.id} value={t.id}>
          {conNombre ? t.nombre : t.nombre}
        </option>
      ))}
    </select>
  );

  const selectorArbitro = (valor: string, pos: number, idx: number) => (
    <select className="input" value={valor} onChange={(e) => setArbitro(idx, pos, e.target.value)}>
      <option value="">{pos === 2 ? 'Sin asignar' : 'Seleccionar…'}</option>
      {referees.map((r) => (
        <option key={r.id} value={r.id}>
          {r.nombreCompleto}
        </option>
      ))}
    </select>
  );

  return (
    <div className="page">
      <h1>Programar semana</h1>
      <p className="page-sub">
        La semana es la unidad de planificación. Se crean solo los partidos de la
        semana elegida (4 por jornada, horarios 19:00–22:00).
      </p>

      {msg && <div className="msg">{msg}</div>}

      <Card>
        <div className="form-grid">
          <label className="field">
            <span className="field-label">Temporada</span>
            <select className="input" value={seasonId} onChange={(e) => setSeasonId(e.target.value)}>
              <option value="">Seleccionar…</option>
              {seasons.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.nombre} ({s.estado})
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            <span className="field-label">Semana</span>
            <select
              className="input"
              value={semana}
              onChange={(e) => setSemana(e.target.value ? Number(e.target.value) : '')}
              disabled={semanas.length === 0}
            >
              <option value="">Seleccionar…</option>
              {semanas.map((s) => (
                <option key={s.semana} value={s.semana}>
                  Semana {s.semana} ({s.inicio} → {s.fin}){s.programada ? ` — ${s.partidos} partidos` : ''}
                </option>
              ))}
            </select>
          </label>
        </div>

        {rango && (
          <p className="page-sub">
            {rango.inicio} → {rango.fin}
            {rondasPendientes !== null && ` · enfrentamientos pendientes: ${rondasPendientes}`}
          </p>
        )}

        <div className="form-actions">
          <Button
            variant="secondary"
            onClick={() => seasonId && semana && cargarSemana(seasonId, Number(semana))}
            disabled={!seasonId || !semana}
          >
            Generar automáticamente
          </Button>
          <Button variant="secondary" onClick={agregarFila} disabled={!rango}>
            Agregar partido manual
          </Button>
          <Button onClick={confirmar} loading={confirmando} disabled={filas.length === 0}>
            Confirmar semana
          </Button>
        </div>
      </Card>

      {cargando && <LoadingState />}

      {!cargando && filas.length > 0 && (
        <div className="list">
          {filas.map((f, idx) => (
            <Card key={idx}>
              <div className="row">
                <div className="row-main">
                  <div className="row-title">Partido {idx + 1}</div>
                  <div className="row-sub">
                    {f.local || '—'} vs {f.visitante || '—'}
                  </div>
                </div>
                <Button variant="danger" onClick={() => quitarFila(idx)}>
                  Quitar
                </Button>
              </div>

              <div className="form-grid">
                <label className="field">
                  <span className="field-label">Fecha</span>
                  <input
                    className="input"
                    type="date"
                    value={f.fecha}
                    onChange={(e) => actualizarFila(idx, { fecha: e.target.value })}
                  />
                </label>
                <label className="field">
                  <span className="field-label">Hora</span>
                  <input
                    className="input"
                    type="time"
                    value={f.hora}
                    onChange={(e) => actualizarFila(idx, { hora: e.target.value })}
                  />
                </label>
                <label className="field">
                  <span className="field-label">Equipo local</span>
                  {selectorEquipos(f.homeTeamId, (v) => {
                    const t = teams.find((x) => x.id === v);
                    actualizarFila(idx, { homeTeamId: v, local: t?.nombreCorto ?? '' });
                  })}
                </label>
                <label className="field">
                  <span className="field-label">Equipo visitante</span>
                  {selectorEquipos(f.awayTeamId, (v) => {
                    const t = teams.find((x) => x.id === v);
                    actualizarFila(idx, { awayTeamId: v, visitante: t?.nombreCorto ?? '' });
                  })}
                </label>
                <label className="field">
                  <span className="field-label">Árbitro 1</span>
                  {selectorArbitro(f.arbitroIds[0] ?? '', 0, idx)}
                </label>
                <label className="field">
                  <span className="field-label">Árbitro 2</span>
                  {selectorArbitro(f.arbitroIds[1] ?? '', 1, idx)}
                </label>
                <label className="field">
                  <span className="field-label">Árbitro 3 (opcional)</span>
                  {selectorArbitro(f.arbitroIds[2] ?? '', 2, idx)}
                </label>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
