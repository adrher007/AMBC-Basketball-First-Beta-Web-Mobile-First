import { api } from './api';
import type { StandingRow } from './types';

export const getStandings = (seasonId: string) =>
  api.get<StandingRow[]>(`/standings/${seasonId}`).then((r) => r.data);
