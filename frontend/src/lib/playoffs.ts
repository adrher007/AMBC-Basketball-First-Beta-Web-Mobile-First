import { api } from './api';
import type { PlayoffSerie } from './types';

export const iniciarPlayoffs = (seasonId: string) =>
  api.post('/admin/playoffs/iniciar', { seasonId }).then((r) => r.data);

export const getPlayoffs = (seasonId: string) =>
  api.get<PlayoffSerie[]>(`/admin/playoffs/estado/${seasonId}`).then((r) => r.data);
