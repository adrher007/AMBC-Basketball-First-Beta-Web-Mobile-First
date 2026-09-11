import { api } from './api';
import type { Game, Roster } from './types';

// ===== Roster / Jugadores =====
export const getRoster = (teamId: string, seasonId: string) =>
  api.get<Roster[]>(`/rosters/equipo/${teamId}/${seasonId}`).then((r) => r.data);

export const createRoster = (data: {
  nombre: string;
  apellido: string;
  posicion: string;
  numero: number;
}) => api.post<Roster>('/rosters', data).then((r) => r.data);

export const updateRosterNumero = (playerId: string, numero: number) =>
  api.put(`/rosters/${playerId}/numero`, { numero });

export const deleteRoster = (playerId: string) =>
  api.delete(`/rosters/${playerId}`);

// ===== Fixture / Partidos =====
export const getGames = (seasonId?: string) =>
  api
    .get<Game[]>('/games', { params: seasonId ? { seasonId } : {} })
    .then((r) => r.data);

// ===== Admin: reprogramar y resultados (Fase 7/8) =====
export const rescheduleGame = (id: string, fecha: string, hora: string) =>
  api.put(`/admin/games/${id}/reschedule`, { fecha, hora });

export const setGameResult = (
  id: string,
  marcadorLocal: number,
  marcadorVisitante: number,
) =>
  api.put(`/admin/games/${id}/result`, { marcadorLocal, marcadorVisitante });
