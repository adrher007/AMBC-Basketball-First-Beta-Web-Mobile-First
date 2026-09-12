import { useEffect, useState } from 'react';
import './Arbitro.module.css';
import Card from '../../components/Card';
import Button from '../../components/Button';
import Input from '../../components/Input';
import Badge from '../../components/Badge';
import { LoadingState, EmptyState, ErrorState } from '../../components/States';
import {
  getPartidosDisponibles,
  crearPlanilla,
  subirImagenPlanilla,
  confirmarPlanilla,
  type PartidoDisponible,
  type Planilla,
} from '../../lib/planillas';
import {
  SCHEMA_VERSION,
  type JugadorCorregido,
  type PlanillaDatosCorregidos,
  type PlanillaExtraida,
} from '../../lib/types';

// ===== 3.1: edición de estadísticas antes de confirmar =====

/** Fila editable de la tabla de jugadores (todo string: '' representa NULL). */
interface Fila {
  numero: string;
  nombre: string;
  apellido: string;
  pts: string;
  dosP: string;
  tresP: string;
  ft: string;
}

const FILA_VACIA: Fila = { numero: '', nombre: '', apellido: '', pts: '', dosP: '', tresP: '', ft: '' };

function numeroDe(v: string): number | null {
  const t = v.trim();
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) ? Math.trunc(n) : null;
}

function dividirNombre(full: string | null | undefined): { nombre: string; apellido: string } {
  const t = (full ?? '').trim();
  if (!t) return { nombre: '', apellido: '' };
  const i = t.lastIndexOf(' ');
  if (i <= 0) return { nombre: t, apellido: '' };
  return { nombre: t.slice(0, i), apellido: t.slice(i + 1) };
}

/**
 * Puntos por jugador derivados de las casillas de ejecución (la planilla AMBC
 * solo registra puntos: 1 = libre, 2 = doble, 3 = triple).
 */
function componentesDesdeCasillas(extraido: PlanillaExtraida, lado: 'A' | 'B') {
  const acc = new Map<number, { ft: number; dosP: number; tresP: number }>();
  for (const c of extraido.scores?.execution?.cells ?? []) {
    if (!c || c.team !== lado) continue;
    if (c.isCrossed === false) continue; // solo casillas marcadas
    if (c.player == null || c.points == null) continue;
    const cur = acc.get(c.player) ?? { ft: 0, dosP: 0, tresP: 0 };
    if (c.points === 1) cur.ft += 1;
    else if (c.points === 2) cur.dosP += 1;
    else if (c.points === 3) cur.tresP += 1;
    acc.set(c.player, cur);
  }
  return acc;
}

/** Filas iniciales a partir del JSON extraído por Groq. */
function filasDesdeExtraido(extraido: PlanillaExtraida | null, lado: 'A' | 'B'): Fila[] {
  if (!extraido) return [];
  const team = lado === 'A' ? extraido.teams?.A : extraido.teams?.B;
  const casillas = componentesDesdeCasillas(extraido, lado);
  return (team?.players ?? []).map((p) => {
    const { nombre, apellido } = dividirNombre(p.name);
    const comp = p.number != null ? casillas.get(p.number) : undefined;
    const ft = comp?.ft ?? 0;
    const dosP = comp?.dosP ?? 0;
    const tresP = comp?.tresP ?? 0;
    return {
      numero: p.number != null ? String(p.number) : '',
      nombre,
      apellido,
      dosP: comp ? String(dosP) : '',
      tresP: comp ? String(tresP) : '',
      ft: comp ? String(ft) : '',
      pts: comp ? String(dosP * 2 + tresP * 3 + ft) : '',
    };
  });
}

/** Filas a partir de correcciones ya guardadas en el backend. */
function filasDesdeCorrecciones(datos: PlanillaDatosCorregidos | null, lado: 'local' | 'visitante'): Fila[] {
  const arr = (lado === 'local' ? datos?.jugadores_local : datos?.jugadores_visitante) ?? [];
  return arr.map((j) => ({
    numero: j.numero != null ? String(j.numero) : '',
    nombre: j.nombre ?? '',
    apellido: j.apellido ?? '',
    pts: j.pts != null ? String(j.pts) : '',
    dosP: j.dos_p != null ? String(j.dos_p) : '',
    tresP: j.tres_p != null ? String(j.tres_p) : '',
    ft: j.ft != null ? String(j.ft) : '',
  }));
}

function parsear<T>(json: string | null | undefined): T | null {
  if (!json) return null;
  try {
    return JSON.parse(json) as T;
  } catch {
    return null;
  }
}

/**
 * Convierte una fila al contrato de `datosCorregidosJson`.
 * Si hay componentes (dos_p/tres_p/ft), Pts se calcula de ellos para no chocar con
 * la validación del backend (Pts == dos_p*2 + tres_p*3 + ft, error 400).
 */
function aJugador(f: Fila): JugadorCorregido {
  const dosP = numeroDe(f.dosP);
  const tresP = numeroDe(f.tresP);
  const ft = numeroDe(f.ft);
  const hayComponentes = dosP != null || tresP != null || ft != null;
  const pts = hayComponentes ? (dosP ?? 0) * 2 + (tresP ?? 0) * 3 + (ft ?? 0) : numeroDe(f.pts);
  const base: JugadorCorregido = {
    numero: numeroDe(f.numero),
    nombre: f.nombre.trim(),
    apellido: f.apellido.trim(),
    pts,
  };
  return hayComponentes ? { ...base, dos_p: dosP, tres_p: tresP, ft } : base;
}

/** Solo las filas mapeables al roster (con dorsal) y con datos. */
function aJugadores(filas: Fila[]): JugadorCorregido[] {
  return filas
    .filter((f) => f.numero.trim() !== '' && (f.nombre.trim() !== '' || f.pts.trim() !== ''))
    .map(aJugador);
}

const sumaPts = (js: JugadorCorregido[]) => js.reduce((t, j) => t + (j.pts ?? 0), 0);

export default function ArbitroPlanillas() {
  const [partidos, setPartidos] = useState<PartidoDisponible[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [msg, setMsg] = useState('');

  // Partido abierto + su planilla
  const [abierto, setAbierto] = useState<PartidoDisponible | null>(null);
  const [planilla, setPlanilla] = useState<Planilla | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [marcadorLocal, setMarcadorLocal] = useState('');
  const [marcadorVisitante, setMarcadorVisitante] = useState('');
  const [confirmando, setConfirmando] = useState(false);

  // 3.1 — Tablas editables (local / visitante) y correcciones guardadas
  const [filasLocal, setFilasLocal] = useState<Fila[]>([]);
  const [filasVisitante, setFilasVisitante] = useState<Fila[]>([]);
  const [correcciones, setCorrecciones] = useState<PlanillaDatosCorregidos | null>(null);

  function load() {
    setLoading(true);
    setError(false);
    getPartidosDisponibles()
      .then(setPartidos)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  /** Carga las tablas desde las correcciones previas o, si no hay, del extracto de Groq. */
  function cargarTablas(plan: Planilla) {
    const corregidas = parsear<PlanillaDatosCorregidos>(plan.datosCorregidosJson);
    const extraido = parsear<PlanillaExtraida>(plan.datosExtraidosJson);
    if (corregidas) {
      setFilasLocal(filasDesdeCorrecciones(corregidas, 'local'));
      setFilasVisitante(filasDesdeCorrecciones(corregidas, 'visitante'));
      setCorrecciones(corregidas);
    } else {
      setFilasLocal(filasDesdeExtraido(extraido, 'A'));
      setFilasVisitante(filasDesdeExtraido(extraido, 'B'));
      setCorrecciones(null);
    }
    // Marcador sugerido desde la puntuación final detectada en la planilla
    const fin = extraido?.scores?.final;
    if (fin?.A != null && !marcadorLocal) setMarcadorLocal(String(fin.A));
    if (fin?.B != null && !marcadorVisitante) setMarcadorVisitante(String(fin.B));
  }

  // Abrir partido: crea la planilla si no existe, o REUTILIZA la de otro árbitro asignado
  async function handleAbrir(p: PartidoDisponible) {
    setMsg('');
    setAbierto(p);
    setPlanilla(null);
    setFile(null);
    setMarcadorLocal('');
    setMarcadorVisitante('');
    setFilasLocal([]);
    setFilasVisitante([]);
    setCorrecciones(null);
    try {
      const plan = await crearPlanilla(p.id);
      setPlanilla(plan);
      cargarTablas(plan);
      if (plan.compartida) {
        setMsg(
          `Esta planilla ya fue creada por ${plan.creadaPor ?? 'otro árbitro asignado'}. Se reutiliza la misma (una sola planilla por partido).`,
        );
      }
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos abrir la planilla.');
    }
  }

  async function handleSubir() {
    if (!planilla || !file) {
      setMsg('Selecciona una imagen de la planilla.');
      return;
    }
    setUploading(true);
    setMsg('');
    try {
      const updated = await subirImagenPlanilla(planilla.id, file);
      setPlanilla(updated);
      cargarTablas(updated);
      if (updated.error) setMsg(updated.error);
      else setMsg('✓ Imagen procesada. Revisa y corrige las estadísticas, luego confirma.');
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos subir la imagen.');
    } finally {
      setUploading(false);
    }
  }

  /** Edita una celda de una tabla. */
  function editarFila(lado: 'local' | 'visitante', idx: number, campo: keyof Fila, valor: string) {
    const setter = lado === 'local' ? setFilasLocal : setFilasVisitante;
    setter((prev) =>
      prev.map((f, i) => {
        if (i !== idx) return f;
        const next = { ...f, [campo]: valor };
        // Si se editan los componentes, Pts se recalcula (evita rechazo del backend)
        if (campo === 'dosP' || campo === 'tresP' || campo === 'ft') {
          const dosP = numeroDe(next.dosP);
          const tresP = numeroDe(next.tresP);
          const ft = numeroDe(next.ft);
          if (dosP != null || tresP != null || ft != null) {
            next.pts = String((dosP ?? 0) * 2 + (tresP ?? 0) * 3 + (ft ?? 0));
          }
        }
        return next;
      }),
    );
    setCorrecciones(null); // hay cambios sin guardar
  }

  function agregarFila(lado: 'local' | 'visitante') {
    const setter = lado === 'local' ? setFilasLocal : setFilasVisitante;
    setter((prev) => [...prev, { ...FILA_VACIA }]);
    setCorrecciones(null);
  }

  function quitarFila(lado: 'local' | 'visitante', idx: number) {
    const setter = lado === 'local' ? setFilasLocal : setFilasVisitante;
    setter((prev) => prev.filter((_, i) => i !== idx));
    setCorrecciones(null);
  }

  const jugadoresLocal = aJugadores(filasLocal);
  const jugadoresVisitante = aJugadores(filasVisitante);
  const sumaLocal = sumaPts(jugadoresLocal);
  const sumaVisitante = sumaPts(jugadoresVisitante);
  const ml = numeroDe(marcadorLocal);
  const mv = numeroDe(marcadorVisitante);
  const avisoSuma =
    (ml != null && jugadoresLocal.length > 0 && sumaLocal !== ml) ||
    (mv != null && jugadoresVisitante.length > 0 && sumaVisitante !== mv);

  /** Guarda las correcciones en estado local (se envían al confirmar). */
  function handleGuardarCorrecciones() {
    if (jugadoresLocal.length === 0 && jugadoresVisitante.length === 0) {
      setMsg('No hay jugadores para guardar. Agrega al menos una fila con dorsal y puntos.');
      return;
    }
    const datos: PlanillaDatosCorregidos = {
      schemaVersion: SCHEMA_VERSION,
      jugadores_local: jugadoresLocal,
      jugadores_visitante: jugadoresVisitante,
    };
    setCorrecciones(datos);
    let aviso = `✓ Correcciones guardadas: ${jugadoresLocal.length} jugadores local (${sumaLocal} pts), ${jugadoresVisitante.length} visitante (${sumaVisitante} pts). Se enviarán al confirmar.`;
    if (ml != null && sumaLocal !== ml) {
      aviso += ` ⚠ La suma de puntos local (${sumaLocal}) no coincide con el marcador (${ml}).`;
    }
    if (mv != null && sumaVisitante !== mv) {
      aviso += ` ⚠ La suma de puntos visitante (${sumaVisitante}) no coincide con el marcador (${mv}).`;
    }
    setMsg(aviso);
  }

  async function handleConfirmar() {
    if (!planilla) return;
    if (marcadorLocal.trim() === '' || marcadorVisitante.trim() === '') {
      setMsg('Ingresa el marcador final (local y visitante).');
      return;
    }
    // Se envían las tablas tal como están (aunque no se haya pulsado "Guardar correcciones")
    const hayFilas = jugadoresLocal.length > 0 || jugadoresVisitante.length > 0;
    const datos: PlanillaDatosCorregidos | undefined = hayFilas
      ? correcciones ?? {
          schemaVersion: SCHEMA_VERSION,
          jugadores_local: jugadoresLocal,
          jugadores_visitante: jugadoresVisitante,
        }
      : correcciones ?? undefined;

    setConfirmando(true);
    setMsg('');
    try {
      await confirmarPlanilla(planilla.id, ml ?? 0, mv ?? 0, datos);
      setMsg(
        datos
          ? '✓ Planilla confirmada. Resultado y estadísticas registrados.'
          : '✓ Planilla confirmada. Resultado registrado (sin estadísticas de jugador).',
      );
      setAbierto(null);
      setPlanilla(null);
      setFile(null);
      setMarcadorLocal('');
      setMarcadorVisitante('');
      setFilasLocal([]);
      setFilasVisitante([]);
      setCorrecciones(null);
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos confirmar la planilla.');
    } finally {
      setConfirmando(false);
    }
  }

  /** Tabla editable de una sección. */
  function tabla(lado: 'local' | 'visitante', titulo: string, filas: Fila[], suma: number) {
    return (
      <div className="arb-confirm">
        <h3>{titulo}</h3>
        <div className="stats-grid">
          <div className="stats-head">Dorsal</div>
          <div className="stats-head">Nombre</div>
          <div className="stats-head">Pts</div>
          <div className="stats-head">DosP</div>
          <div className="stats-head">TresP</div>
          <div className="stats-head">Ft</div>
          <div className="stats-head" />
          {filas.map((f, i) => (
            <div className="stats-row" key={`${lado}-${i}`}>
              <input
                className="stats-input"
                inputMode="numeric"
                value={f.numero}
                onChange={(e) => editarFila(lado, i, 'numero', e.target.value)}
                placeholder="#"
              />
              <input
                className="stats-input"
                value={f.nombre}
                onChange={(e) => editarFila(lado, i, 'nombre', e.target.value)}
                placeholder="Nombre"
              />
              <input
                className="stats-input"
                inputMode="numeric"
                value={f.pts}
                onChange={(e) => editarFila(lado, i, 'pts', e.target.value)}
                placeholder="—"
              />
              <input
                className="stats-input"
                inputMode="numeric"
                value={f.dosP}
                onChange={(e) => editarFila(lado, i, 'dosP', e.target.value)}
                placeholder="—"
              />
              <input
                className="stats-input"
                inputMode="numeric"
                value={f.tresP}
                onChange={(e) => editarFila(lado, i, 'tresP', e.target.value)}
                placeholder="—"
              />
              <input
                className="stats-input"
                inputMode="numeric"
                value={f.ft}
                onChange={(e) => editarFila(lado, i, 'ft', e.target.value)}
                placeholder="—"
              />
              <button className="stats-del" type="button" onClick={() => quitarFila(lado, i)}>
                ×
              </button>
            </div>
          ))}
        </div>
        <div className="arb-actions">
          <Button variant="secondary" onClick={() => agregarFila(lado)}>
            Agregar jugador
          </Button>
          <span className="arb-sub">
            {filas.length} jugadores · suma {suma} pts
          </span>
        </div>
      </div>
    );
  }

  const estadoColor = (e: string): 'default' | 'accent' | 'success' =>
    e === 'CONFIRMADA' ? 'success' : e === 'PROCESADA' ? 'accent' : 'default';

  return (
    <div className="page">
      <h1>Mis partidos</h1>

      {msg && <div className="arb-msg">{msg}</div>}

      {/* ===== Partido abierto + planilla ===== */}
      {abierto && planilla && (
        <Card>
          <div className="arb-row">
            <div className="arb-main">
              <div className="arb-title">
                {abierto.local} vs {abierto.visitante}
              </div>
              <div className="arb-sub">
                {abierto.fecha} · {abierto.hora}
              </div>
            </div>
            <Badge color={estadoColor(planilla.estado)}>{planilla.estado}</Badge>
          </div>

          {planilla.compartida && (
            <div className="arb-sub">
              Planilla compartida — creada por {planilla.creadaPor ?? 'otro árbitro asignado'}
            </div>
          )}

          <h3>Planilla</h3>
          <input
            type="file"
            accept="image/*"
            capture="environment"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
          <div className="arb-actions">
            <Button onClick={handleSubir} loading={uploading} disabled={!file}>
              Procesar imagen
            </Button>
            <Button variant="secondary" onClick={() => { setAbierto(null); setPlanilla(null); }}>
              Cerrar
            </Button>
          </div>

          {planilla.imagenUrl && (
            <div className="arb-preview">
              <img src={planilla.imagenUrl} alt="Planilla" />
            </div>
          )}

          {planilla.estado === 'PROCESADA' && (
            <>
              {/* ===== 3.1: edición de estadísticas por jugador ===== */}
              <div className="arb-confirm">
                <h3>Estadísticas por jugador</h3>
                <div className="arb-sub">
                  Precargadas desde la planilla. Corrige dorsales, nombres y puntos; la
                  suma de puntos por equipo debe coincidir con el marcador final.
                </div>
                {filasLocal.length === 0 && filasVisitante.length === 0 && (
                  <div className="arb-sub">
                    La planilla no tiene jugadores detectados. Puedes agregarlos manualmente.
                  </div>
                )}
              </div>

              {tabla('local', `Jugadores Local · ${abierto.local}`, filasLocal, sumaLocal)}
              {tabla('visitante', `Jugadores Visitante · ${abierto.visitante}`, filasVisitante, sumaVisitante)}

              <div className="arb-actions">
                <Button variant="secondary" onClick={handleGuardarCorrecciones}>
                  Guardar correcciones
                </Button>
                {correcciones && <span className="arb-sub">✓ Correcciones guardadas (listas para enviar)</span>}
              </div>

              <div className="arb-confirm">
                <h3>Confirmar resultado</h3>
                <div className="arb-scores">
                  <Input
                    label="Marcador local"
                    type="number"
                    value={marcadorLocal}
                    onChange={setMarcadorLocal}
                    placeholder="0"
                  />
                  <Input
                    label="Marcador visitante"
                    type="number"
                    value={marcadorVisitante}
                    onChange={setMarcadorVisitante}
                    placeholder="0"
                  />
                </div>
                {avisoSuma && (
                  <div className="arb-warn">
                    La suma de puntos por equipo no coincide con el marcador (local {sumaLocal}
                    {ml != null ? ` vs ${ml}` : ''} · visitante {sumaVisitante}
                    {mv != null ? ` vs ${mv}` : ''}). Puedes confirmar igual, pero el servidor
                    podría rechazarlo.
                  </div>
                )}
                <Button onClick={handleConfirmar} loading={confirmando}>
                  Confirmar planilla
                </Button>
              </div>
            </>
          )}
        </Card>
      )}

      {/* ===== Mis partidos asignados ===== */}
      {loading && <LoadingState />}
      {!loading && error && <ErrorState label="tus partidos" onRetry={load} />}
      {!loading && !error && partidos.length === 0 && (
        <EmptyState label="partidos asignados" />
      )}
      {!loading && !error && partidos.length > 0 && (
        <div className="list">
          {partidos.map((p) => (
            <Card key={p.id}>
              <div className="arb-row">
                <div className="arb-main">
                  <div className="arb-title">
                    {p.local} vs {p.visitante}
                  </div>
                  <div className="arb-sub">
                    {p.fecha} · {p.hora} · {p.fase}
                  </div>
                  <div className="arb-sub">
                    {p.arbitros.length} árbitros asignados
                  </div>
                  <div className="arb-sub">
                    {p.arbitros.map((a) => (
                      <div key={a.id}>
                        ✓ {a.tu ? 'Tú' : a.nombre}
                      </div>
                    ))}
                  </div>
                  {p.planillaEstado && (
                    <div className="arb-sub">Planilla: {p.planillaEstado}</div>
                  )}
                </div>
                <Button onClick={() => handleAbrir(p)}>Abrir partido</Button>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
