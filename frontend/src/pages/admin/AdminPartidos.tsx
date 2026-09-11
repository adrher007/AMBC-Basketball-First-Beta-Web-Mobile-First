import { useEffect, useState, useCallback, useMemo } from 'react';
import './Admin.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import Badge from '../../components/Badge';
import { LoadingState, ErrorState, EmptyState } from '../../components/States';
import { getSeasons } from '../../lib/catalog';
import { getReferees } from '../../lib/staff';
import { getAdminGames, setArbitrosPartido, type PartidoAdmin } from '../../lib/adminGames';
import type { Season, Referee } from '../../lib/types';

const MIN_ARB = 2;
const MAX_ARB = 3;

export default function AdminPartidos() {
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [referees, setReferees] = useState<Referee[]>([]);
  const [seasonId, setSeasonId] = useState('');
  const [partidos, setPartidos] = useState<PartidoAdmin[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [msg, setMsg] = useState('');
  const [filtro, setFiltro] = useState<'TODOS' | 'SCHEDULED' | 'FINISHED'>('TODOS');

  // ===== Filtros de gestión =====
  const [busqueda, setBusqueda] = useState('');
  const [semanaFiltro, setSemanaFiltro] = useState<number | ''>('');
  const [fechaFiltro, setFechaFiltro] = useState('');

  // ===== Edición de árbitros =====
  const [editando, setEditando] = useState<string | null>(null);
  const [verPartido, setVerPartido] = useState<string | null>(null);
  const [selArb, setSelArb] = useState<string[]>(['', '']);
  const [guardando, setGuardando] = useState(false);

  const load = useCallback(async (sid?: string) => {
    try {
      setLoading(true);
      setError(false);
      const [games, refs] = await Promise.all([
        getAdminGames(sid || undefined),
        getReferees(),
      ]);
      setPartidos(games);
      setReferees(refs);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    getSeasons().then(setSeasons).catch(() => {});
    load();
  }, [load]);

  // Semanas disponibles según los partidos cargados (de la temporada filtrada)
  const semanas = useMemo(
    () => Array.from(new Set(partidos.map((p) => p.semana))).sort((a, b) => a - b),
    [partidos],
  );

  function abrirEdicion(p: PartidoAdmin) {
    setEditando(p.id);
    setMsg('');
    setSelArb([p.arbitros[0]?.id ?? '', p.arbitros[1]?.id ?? '', p.arbitros[2]?.id ?? '']);
  }

  async function guardarArbitros(p: PartidoAdmin) {
    const ids = selArb.filter(Boolean);
    if (ids.length < MIN_ARB) {
      setMsg(`Se requieren al menos ${MIN_ARB} árbitros.`);
      return;
    }
    if (ids.length > MAX_ARB) {
      setMsg(`Máximo ${MAX_ARB} árbitros.`);
      return;
    }
    if (new Set(ids).size !== ids.length) {
      setMsg('No puedes asignar el mismo árbitro dos veces.');
      return;
    }
    setGuardando(true);
    try {
      await setArbitrosPartido(p.id, ids);
      setMsg(`✓ Árbitros actualizados en ${p.local} vs ${p.visitante}.`);
      setEditando(null);
      load(seasonId);
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos actualizar los árbitros.');
    } finally {
      setGuardando(false);
    }
  }

  const visibles = partidos.filter((p) => {
    if (filtro !== 'TODOS' && p.estado !== filtro) return false;
    if (semanaFiltro !== '' && p.semana !== semanaFiltro) return false;
    if (fechaFiltro && p.fecha !== fechaFiltro) return false;

    if (busqueda.trim()) {
      const q = busqueda.trim().toLowerCase();
      const texto = [
        p.local,
        p.visitante,
        p.fase,
        ...p.arbitros.map((a) => a.nombre),
      ]
        .join(' ')
        .toLowerCase();
      if (!texto.includes(q)) return false;
    }
    return true;
  });

  const opcionesArbitro = (valor: string, idx: number) => (
    <select
      className="input"
      value={valor}
      onChange={(e) => {
        const copia = [...selArb];
        copia[idx] = e.target.value;
        setSelArb(copia);
      }}
    >
      <option value="">{idx === 2 ? 'Sin asignar (opcional)' : 'Seleccionar…'}</option>
      {referees.map((r) => (
        <option key={r.id} value={r.id}>
          {r.nombreCompleto}
        </option>
      ))}
    </select>
  );

  return (
    <div className="page">
      <div className="head-row">
        <h1>Partidos</h1>
        <select
          className="filter"
          value={seasonId}
          onChange={(e) => {
            setSeasonId(e.target.value);
            setSemanaFiltro('');
            load(e.target.value);
          }}
        >
          <option value="">Todas las temporadas</option>
          {seasons.map((s) => (
            <option key={s.id} value={s.id}>
              {s.nombre}
            </option>
          ))}
        </select>
      </div>

      {msg && <div className="msg">{msg}</div>}

      {/* ===== Búsqueda + filtros ===== */}
      <Card>
        <div className="form-grid">
          <label className="field">
            <span className="field-label">Buscar</span>
            <input
              className="input"
              value={busqueda}
              onChange={(e) => setBusqueda(e.target.value)}
              placeholder="Equipo, fase o árbitro…"
            />
          </label>
          <label className="field">
            <span className="field-label">Semana</span>
            <select
              className="input"
              value={semanaFiltro}
              onChange={(e) => setSemanaFiltro(e.target.value ? Number(e.target.value) : '')}
            >
              <option value="">Todas las semanas</option>
              {semanas.map((s) => (
                <option key={s} value={s}>
                  Semana {s}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            <span className="field-label">Fecha</span>
            <input
              className="input"
              type="date"
              value={fechaFiltro}
              onChange={(e) => setFechaFiltro(e.target.value)}
            />
          </label>
        </div>

        <div className="form-actions">
          <Button
            variant="secondary"
            onClick={() => {
              setBusqueda('');
              setSemanaFiltro('');
              setFechaFiltro('');
              setFiltro('TODOS');
            }}
          >
            Limpiar filtros
          </Button>
        </div>
      </Card>

      <div className="row-actions">
        {(['TODOS', 'SCHEDULED', 'FINISHED'] as const).map((f) => (
          <Button
            key={f}
            variant={filtro === f ? 'primary' : 'secondary'}
            onClick={() => setFiltro(f)}
          >
            {f === 'TODOS' ? 'Todos' : f === 'SCHEDULED' ? 'Pendientes' : 'Finalizados'}
          </Button>
        ))}
        <span className="page-sub">
          {visibles.length} de {partidos.length} partidos
        </span>
      </div>

      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="los partidos" onRetry={() => load(seasonId)} />}
      {!loading && !error && visibles.length === 0 && (
        <EmptyState label="partidos con estos filtros" />
      )}
      {!loading && !error && visibles.length > 0 && (
        <div className="list">
          {visibles.map((p) => (
            <Card key={p.id}>
              <div className="row">
                <div className="row-main">
                  <div className="row-title">
                    {p.local} vs {p.visitante}
                  </div>
                  <div className="row-sub">
                    Semana {p.semana} · {p.fecha} · {p.hora} · {p.fase}
                  </div>
                  {p.estado === 'FINISHED' && (
                    <div className="row-sub">
                      Resultado: {p.marcadorLocal} – {p.marcadorVisitante}
                    </div>
                  )}

                  <div className="arb-list">
                    <div className="row-sub">
                      <strong>Árbitros asignados</strong>
                      {p.arbitros.length === 0 && ' — ninguno'}
                    </div>
                    {p.arbitros.map((a) => (
                      <div key={a.id} className="arb-item">
                        ✓ {a.nombre}
                      </div>
                    ))}
                    {p.planillaEstado && (
                      <div className="arb-item">Planilla: {p.planillaEstado}</div>
                    )}
                  </div>
                </div>

                <div className="row-actions">
                  <Badge color={p.estado === 'FINISHED' ? 'default' : 'accent'}>
                    {p.estado === 'FINISHED' ? 'Finalizado' : 'Pendiente'}
                  </Badge>
                  <Button
                    variant="secondary"
                    onClick={() => setVerPartido(verPartido === p.id ? null : p.id)}
                  >
                    {verPartido === p.id ? 'Ocultar' : 'Ver partido'}
                  </Button>
                  <Button
                    variant="secondary"
                    onClick={() => (editando === p.id ? setEditando(null) : abrirEdicion(p))}
                  >
                    {editando === p.id ? 'Cancelar' : 'Modificar árbitros'}
                  </Button>
                </div>
              </div>

              {/* Detalle del partido */}
              {verPartido === p.id && (
                <div className="inline-form">
                  <div className="field">
                    <span className="field-label">Identificador</span>
                    <span className="row-sub">{p.id}</span>
                  </div>
                  <div className="field">
                    <span className="field-label">Temporada</span>
                    <span className="row-sub">{seasons.find((s) => s.id === p.seasonId)?.nombre ?? p.seasonId}</span>
                  </div>
                  <div className="field">
                    <span className="field-label">Planilla</span>
                    <span className="row-sub">{p.planillaEstado ?? 'sin planilla'}</span>
                  </div>
                  <div className="field">
                    <span className="field-label">Árbitros</span>
                    <span className="row-sub">
                      {p.arbitros.length > 0 ? p.arbitros.map((a) => a.nombre).join(', ') : 'ninguno'}
                    </span>
                  </div>
                </div>
              )}

              {/* Editor de árbitros (siempre 2..3) */}
              {editando === p.id && (
                <div className="inline-form">
                  <label className="field">
                    <span className="field-label">Árbitro 1</span>
                    {opcionesArbitro(selArb[0] ?? '', 0)}
                  </label>
                  <label className="field">
                    <span className="field-label">Árbitro 2</span>
                    {opcionesArbitro(selArb[1] ?? '', 1)}
                  </label>
                  <label className="field">
                    <span className="field-label">Árbitro 3 (opcional)</span>
                    {opcionesArbitro(selArb[2] ?? '', 2)}
                  </label>
                  <Button onClick={() => guardarArbitros(p)} loading={guardando}>
                    Guardar árbitros
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
