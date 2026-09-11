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

  function load() {
    setLoading(true);
    setError(false);
    getPartidosDisponibles()
      .then(setPartidos)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  // Abrir partido: crea la planilla si no existe, o REUTILIZA la de otro árbitro asignado
  async function handleAbrir(p: PartidoDisponible) {
    setMsg('');
    setAbierto(p);
    setPlanilla(null);
    setFile(null);
    setMarcadorLocal('');
    setMarcadorVisitante('');
    try {
      const plan = await crearPlanilla(p.id);
      setPlanilla(plan);
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
      if (updated.error) setMsg(updated.error);
      else setMsg('✓ Imagen procesada. Revisa y confirma.');
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos subir la imagen.');
    } finally {
      setUploading(false);
    }
  }

  async function handleConfirmar() {
    if (!planilla) return;
    if (!marcadorLocal || !marcadorVisitante) {
      setMsg('Ingresa el marcador final (local y visitante).');
      return;
    }
    setConfirmando(true);
    setMsg('');
    try {
      await confirmarPlanilla(
        planilla.id,
        parseInt(marcadorLocal, 10),
        parseInt(marcadorVisitante, 10),
      );
      setMsg('✓ Planilla confirmada. Resultado registrado.');
      setAbierto(null);
      setPlanilla(null);
      setFile(null);
      setMarcadorLocal('');
      setMarcadorVisitante('');
      load();
    } catch (err: any) {
      setMsg(err?.response?.data?.error ?? 'No pudimos confirmar la planilla.');
    } finally {
      setConfirmando(false);
    }
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
              <Button onClick={handleConfirmar} loading={confirmando}>
                Confirmar planilla
              </Button>
            </div>
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
