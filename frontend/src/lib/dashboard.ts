import { api } from './api';

export interface DashboardData {
  temporadaActiva: { id: string; nombre: string; estado: string } | null;
  totalEquipos: number;
  totalJugadores: number;
  totalCoaches: number;
  totalArbitros: number;
  totalPartidos: number;
  partidosPendientes: number;
  avisosActivos: number;
}

export const getDashboard = () =>
  api.get<DashboardData>('/admin/dashboard').then((r) => r.data);
