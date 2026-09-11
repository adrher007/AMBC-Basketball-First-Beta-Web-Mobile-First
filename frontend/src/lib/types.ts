export type SeasonEstado = 'UPCOMING' | 'ACTIVE' | 'FINISHED';

export interface Season {
  id: string;
  nombre: string;
  fechaInicio: string; // YYYY-MM-DD
  fechaFin: string;
  estado: SeasonEstado;
}

export interface Team {
  id: string;
  nombre: string;
  nombreCorto: string;
  logoUrl?: string | null;
}

export interface TeamDetalle {
  id: string;
  nombre: string;
  nombreCorto: string;
  logoUrl?: string | null;
  temporada?: string | null;
  plantilla: {
    id: string;
    nombre: string;
    apellido: string;
    posicion: string;
    numero: number;
  }[];
  cuerpoTecnico: { nombre: string; rol: string }[];
}

export type Posicion = 'BASE' | 'ESCOLTA' | 'ALERO' | 'ALA_PIVOT' | 'PIVOT';

export interface Player {
  id: string;
  nombre: string;
  apellido: string;
  posicion: Posicion;
}

export interface Roster {
  playerId: string;
  nombre: string;
  apellido: string;
  posicion: Posicion;
  numero: number;
  activo: boolean;
}

export type GameEstado = 'SCHEDULED' | 'FINISHED';

export interface Game {
  id: string;
  seasonId: string;
  fase: string;
  playoffSeriesId?: string | null;
  homeTeamId: string;
  awayTeamId: string;
  fecha: string;
  hora: string;
  estado: GameEstado;
  marcadorLocal: number;
  marcadorVisitante: number;
  ganadorId?: string | null;
}

export interface StandingRow {
  posicion: number;
  equipo: string;
  nombreCorto: string;
  pj: number;
  victorias: number;
  derrotas: number;
  pts: number;
  pf: number;
  pc: number;
  diff: number;
}

export interface PlayoffSerie {
  id: string;
  fase: string; // QF | SF | FINAL | TERCER_LUGAR
  teamAId: string;
  teamBId: string;
  winsA: number;
  winsB: number;
  bestOf: number;
  ganadorId?: string | null;
  estado: string;
}

export interface Coach {
  id: string;
  userId: string;
  nombreCompleto: string;
  username: string;
  teamId: string;
  teamNombre: string;
  seasonId: string;
}

export interface Referee {
  id: string;
  userId: string;
  nombreCompleto: string;
  username: string;
}

export interface MiEquipo {
  teamId: string;
  teamNombre: string;
  teamNombreCorto: string;
  logoUrl?: string | null;
  seasonId: string;
  seasonNombre: string;
}

export interface Notification {
  id: string;
  mensaje: string;
  destinatarios: string;
  creadoEn: string;
  vista: boolean;
}
