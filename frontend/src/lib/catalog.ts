import { api } from './api';
import type { Season, Team, TeamDetalle } from './types';

export const getSeasons = () => api.get<Season[]>('/seasons').then((r) => r.data);
export const getSeason = (id: string) =>
  api.get<Season>(`/seasons/${id}`).then((r) => r.data);
export const createSeason = (data: {
  nombre: string;
  fechaInicio: string;
  fechaFin: string;
  estado?: string;
}) => api.post<Season>('/seasons', data).then((r) => r.data);
export const updateSeason = (
  id: string,
  data: { nombre: string; fechaInicio: string; fechaFin: string },
) => api.put(`/seasons/${id}`, data);
export const activateSeason = (id: string) =>
  api.post(`/seasons/${id}/activar`);
export const finishSeason = (id: string) =>
  api.post(`/seasons/${id}/finalizar`);

export const getTeams = () => api.get<Team[]>('/teams').then((r) => r.data);
export const getTeam = (id: string) =>
  api.get<Team>(`/teams/${id}`).then((r) => r.data);
export const getTeamDetalle = (id: string) =>
  api.get<TeamDetalle>(`/teams/${id}/detalle`).then((r) => r.data);
export const createTeam = (data: { nombre: string; nombreCorto: string }) =>
  api.post<Team>('/teams', data).then((r) => r.data);
export const updateTeam = (id: string, data: { nombre: string; nombreCorto: string }) =>
  api.put(`/teams/${id}`, data);
export const deleteTeam = (id: string) => api.delete(`/teams/${id}`);
