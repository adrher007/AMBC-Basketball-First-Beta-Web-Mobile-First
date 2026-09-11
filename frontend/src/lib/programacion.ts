import { api } from './api';

export interface SemanaInfo {
  semana: number;
  inicio: string;
  fin: string;
  partidos: number;
  programada: boolean;
}

export interface SemanasResponse {
  totalSemanas: number;
  inicioTemporada: string;
  finTemporada: string;
  semanas: SemanaInfo[];
}

export interface PartidoSemana {
  fecha: string;
  hora: string;
  homeTeamId: string;
  awayTeamId: string;
  local: string;
  visitante: string;
  arbitroIds: string[];
}

export interface PropuestaResponse {
  semana: number;
  inicio: string;
  fin: string;
  rondasPendientes: number;
  propuesta: PartidoSemana[];
}

export const getSemanas = (seasonId: string) =>
  api
    .get<SemanasResponse>('/admin/programacion/semanas', { params: { seasonId } })
    .then((r) => r.data);

export const getPropuestaSemana = (seasonId: string, semana: number) =>
  api
    .get<PropuestaResponse>('/admin/programacion/pendientes', {
      params: { seasonId, semana },
    })
    .then((r) => r.data);

export const confirmarSemana = (data: {
  seasonId: string;
  semana: number;
  partidos: {
    fecha: string;
    hora: string;
    homeTeamId: string;
    awayTeamId: string;
    arbitroIds: string[];
  }[];
}) => api.post<{ semana: number; partidos: number; ids: string[] }>('/admin/programacion/semana', data).then((r) => r.data);
