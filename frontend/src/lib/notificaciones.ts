import { api } from './api';
import type { Notification } from './types';

export const getNotificaciones = () =>
  api.get<Notification[]>('/notificaciones').then((r) => r.data);

export const marcarNotificacionVista = (id: string) =>
  api.post(`/notificaciones/${id}/visto`);

export const getNotificacionesFan = () =>
  api.get<Notification[]>('/notificaciones/fan').then((r) => r.data);

export const crearNotificacion = (mensaje: string, destinatarios: string) =>
  api.post<Notification>('/admin/notificaciones', { mensaje, destinatarios }).then((r) => r.data);
