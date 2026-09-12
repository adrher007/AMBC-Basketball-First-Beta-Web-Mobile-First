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

// ===== 3.1: corrección de estadísticas de la planilla =====

/** Versión del esquema del JSON de corrección. */
export const SCHEMA_VERSION = 1;

/** Jugador dentro de `datosCorregidosJson` (contrato que consume StatsMapper). */
export interface JugadorCorregido {
  numero: number | null;
  nombre: string;
  apellido: string;
  /** Puntos totales. Debe coincidir con `dos_p*2 + tres_p*3 + ft` si vienen los componentes. */
  pts?: number | null;
  dos_p?: number | null;
  tres_p?: number | null;
  ft?: number | null;
}

/** Payload que espera `POST /planillas/{id}/confirmar` en `datosCorregidosJson`. */
export interface PlanillaDatosCorregidos {
  schemaVersion: number;
  jugadores_local: JugadorCorregido[];
  jugadores_visitante: JugadorCorregido[];
}

/** JSON que devuelve Groq en `datosExtraidosJson` (solo las partes que usa la UI). */
export interface PlanillaExtraida {
  schemaVersion?: number;
  teams?: {
    A?: { name?: string | null; players?: { number?: number | null; name?: string | null }[] };
    B?: { name?: string | null; players?: { number?: number | null; name?: string | null }[] };
  };
  scores?: {
    execution?: {
      cells?: {
        team?: string | null;
        player?: number | null;
        points?: number | null;
        isCrossed?: boolean | null;
      }[];
    };
    final?: { A?: number | null; B?: number | null };
  };
}
