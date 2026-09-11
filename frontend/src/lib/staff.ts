import { api } from './api';
import type { Coach, MiEquipo, Referee } from './types';

export const getCoaches = () => api.get<Coach[]>('/admin/coaches').then((r) => r.data);
export const createCoach = (data: { nombreCompleto: string; teamId: string; seasonId: string }) =>
  api.post<Coach>('/admin/coaches', data).then((r) => r.data);
export const deleteCoach = (id: string) => api.delete(`/admin/coaches/${id}`);
export const resetCoachPassword = (id: string, password: string) =>
  api.put(`/admin/coaches/${id}/password`, { password });

export const getReferees = () => api.get<Referee[]>('/admin/referees').then((r) => r.data);
export const createReferee = (data: { nombreCompleto: string; password: string }) =>
  api.post<Referee>('/admin/referees', data).then((r) => r.data);
export const deleteReferee = (id: string) => api.delete(`/admin/referees/${id}`);
export const resetRefereePassword = (id: string, password: string) =>
  api.put(`/admin/referees/${id}/password`, { password });

export const getMiEquipo = () => api.get<MiEquipo>('/entrenador/mi-equipo').then((r) => r.data);

export const subirLogoEquipo = (file: File) => {
  const fd = new FormData();
  fd.append('file', file);
  return api
    .post<{ logoUrl: string }>('/entrenador/logo', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    .then((r) => r.data);
};
