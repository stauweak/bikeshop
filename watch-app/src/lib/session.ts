import { haversine, speedBetween, type TrackPoint } from './geo'

/** Points moins précis que ce seuil (mètres) sont ignorés pour le tracé. */
const MAX_ACCURACY_M = 50
/** En dessous de cette distance (mètres), on considère que l'on n'a pas bougé (bruit GPS). */
const MIN_STEP_M = 2
/** Vitesse minimale (m/s) comptée comme « en mouvement » pour le temps de déplacement. */
const MOVING_THRESHOLD_MS = 0.5

export interface SessionStats {
  /** Distance cumulée en mètres */
  distance: number
  /** Durée totale (chrono) en ms */
  elapsed: number
  /** Temps en mouvement en ms */
  movingTime: number
  /** Vitesse instantanée en m/s */
  speed: number
  /** Vitesse moyenne (distance / temps en mouvement) en m/s */
  avgSpeed: number
  /** Vitesse max en m/s */
  maxSpeed: number
  /** Altitude actuelle en mètres, si connue */
  altitude: number | null
  /** Dénivelé positif cumulé en mètres */
  elevationGain: number
}

export const EMPTY_STATS: SessionStats = {
  distance: 0,
  elapsed: 0,
  movingTime: 0,
  speed: 0,
  avgSpeed: 0,
  maxSpeed: 0,
  altitude: null,
  elevationGain: 0,
}

/**
 * Accumule les points GPS d'une séance et calcule les statistiques
 * (distance, vitesses, dénivelé…) au fil de l'eau.
 */
export class TrackSession {
  readonly points: TrackPoint[] = []
  private stats: SessionStats = { ...EMPTY_STATS }
  private startedAt: number | null = null
  /** Durée accumulée avant la dernière pause */
  private elapsedBeforePause = 0
  private lastAlt: number | null = null

  start(now: number): void {
    if (this.startedAt == null) this.startedAt = now
  }

  pause(now: number): void {
    if (this.startedAt != null) {
      this.elapsedBeforePause += now - this.startedAt
      this.startedAt = null
      this.stats.speed = 0
    }
  }

  get isRunning(): boolean {
    return this.startedAt != null
  }

  /** Statistiques avec le chrono mis à jour à l'instant `now`. */
  snapshot(now: number): SessionStats {
    const running = this.startedAt != null ? now - this.startedAt : 0
    return { ...this.stats, elapsed: this.elapsedBeforePause + running }
  }

  addPoint(p: TrackPoint): void {
    if (!this.isRunning) return
    if (p.accuracy > MAX_ACCURACY_M) return

    const prev = this.points[this.points.length - 1]
    this.points.push(p)

    // Vitesse instantanée : capteur si dispo, sinon dérivée des positions.
    let speed = p.speed
    if (speed == null && prev) speed = speedBetween(prev, p)
    if (speed != null) {
      this.stats.speed = speed
      if (speed > this.stats.maxSpeed) this.stats.maxSpeed = speed
    }

    if (prev) {
      const step = haversine(prev.lat, prev.lon, p.lat, p.lon)
      if (step >= MIN_STEP_M) this.stats.distance += step
      if ((speed ?? 0) >= MOVING_THRESHOLD_MS) {
        this.stats.movingTime += p.time - prev.time
      }
    }

    if (p.alt != null) {
      this.stats.altitude = p.alt
      if (this.lastAlt != null && p.alt > this.lastAlt) {
        this.stats.elevationGain += p.alt - this.lastAlt
      }
      this.lastAlt = p.alt
    }

    const movingSec = this.stats.movingTime / 1000
    this.stats.avgSpeed = movingSec > 0 ? this.stats.distance / movingSec : 0
  }
}
