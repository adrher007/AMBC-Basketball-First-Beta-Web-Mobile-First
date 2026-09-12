import { api } from './api';
import type { PlanillaDatosCorregidos } from './types';

export interface ArbitroAsignado {
  id: string;
  nombre: string;
  tu: boolean;
}

export interface PartidoDisponible {
  id: string;
  fecha: string;
  hora: string;
  local: string;
  visitante: string;
  fase: string;
  estado: string;
  arbitros: ArbitroAsignado[];
  planillaEstado: string | null;
}

export interface Planilla {
  id: string;
  gameId: string;
  estado: string;
  imagenUrl?: string | null;
  datosExtraidosJson?: string | null;
  datosCorregidosJson?: string | null;
  error?: string | null;
  marcadorLocal?: string | null;
  marcadorVisitante?: string | null;
  creadaPor?: string | null;
  compartida?: boolean;
}

export const getPartidosDisponibles = () =>
  api.get<PartidoDisponible[]>('/planillas/partidos').then((r) => r.data);

export const crearPlanilla = (gameId: string) =>
  api.post<Planilla>('/planillas', { gameId }).then((r) => r.data);

export const subirImagenPlanilla = (id: string, file: File) => {
  const fd = new FormData();
  fd.append('file', file);
  return api
    .post<Planilla>(`/planillas/${id}/imagen`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    .then((r) => r.data);
};

export const getPlanilla = (id: string) =>
  api.get<Planilla>(`/planillas/${id}`).then((r) => r.data);

// 3.1 — Los tipos del JSON de corrección viven en lib/types.ts
export { SCHEMA_VERSION } from './types';
export type { JugadorCorregido, PlanillaDatosCorregidos } from './types';

export const confirmarPlanilla = (
  id: string,
  marcadorLocal: number,
  marcadorVisitante: number,
  datos?: PlanillaDatosCorregidos,
) =>
  api
    .post(`/planillas/${id}/confirmar`, {
      marcadorLocal,
      marcadorVisitante,
      ...(datos ? { datosCorregidosJson: JSON.stringify(datos) } : {}),
    })
    .then((r) => r.data);
