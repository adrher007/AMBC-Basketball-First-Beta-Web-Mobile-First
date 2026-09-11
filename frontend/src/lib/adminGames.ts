import { api } from './api';

export interface ArbitroAsignado {
  id: string;
  nombre: string;
}

export interface PartidoAdmin {
  id: string;
  seasonId: string;
  fase: string;
  fecha: string;
  hora: string;
  estado: string;
  semana: number;
  local: string;
  visitante: string;
  homeTeamId: string;
  awayTeamId: string;
  marcadorLocal: number | null;
  marcadorVisitante: number | null;
  arbitros: ArbitroAsignado[];
  planillaEstado: string | null;
}

export const getAdminGames = (seasonId?: string) =>
  api
    .get<PartidoAdmin[]>('/admin/games', { params: seasonId ? { seasonId } : {} })
    .then((r) => r.data);

export const crearPartido = (data: {
  seasonId: string;
  fecha: string;
  hora: string;
  homeTeamId: string;
  awayTeamId: string;
  arbitroIds: string[];
}) => api.post<{ id: string; arbitros: number }>('/admin/games', data).then((r) => r.data);

export const setArbitrosPartido = (gameId: string, arbitroIds: string[]) =>
  api.put<{ id: string; arbitros: number }>(`/admin/games/${gameId}/arbitros`, { arbitroIds }).then((r) => r.data);
