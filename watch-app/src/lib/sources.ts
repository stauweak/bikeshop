import type { TrackPoint } from './geo'

export type PositionCallback = (p: TrackPoint) => void
export type ErrorCallback = (message: string) => void

export interface PositionSource {
  start(onPoint: PositionCallback, onError: ErrorCallback): void
  stop(): void
}

/** Source réelle : GPS de l'appareil via l'API Geolocation. */
export class GpsSource implements PositionSource {
  private watchId: number | null = null

  start(onPoint: PositionCallback, onError: ErrorCallback): void {
    if (!('geolocation' in navigator)) {
      onError('GPS non disponible sur cet appareil')
      return
    }
    this.watchId = navigator.geolocation.watchPosition(
      (pos) => {
        onPoint({
          lat: pos.coords.latitude,
          lon: pos.coords.longitude,
          alt: pos.coords.altitude,
          time: pos.timestamp,
          accuracy: pos.coords.accuracy,
          speed: pos.coords.speed,
        })
      },
      (err) => {
        const messages: Record<number, string> = {
          [err.PERMISSION_DENIED]: 'Accès à la position refusé',
          [err.POSITION_UNAVAILABLE]: 'Position indisponible',
          [err.TIMEOUT]: 'Délai GPS dépassé',
        }
        onError(messages[err.code] ?? 'Erreur GPS')
      },
      { enableHighAccuracy: true, maximumAge: 1000, timeout: 15000 },
    )
  }

  stop(): void {
    if (this.watchId != null) {
      navigator.geolocation.clearWatch(this.watchId)
      this.watchId = null
    }
  }
}

/**
 * Source simulée : parcourt une boucle autour d'un point de départ,
 * pratique pour tester l'application sans capteur GPS.
 */
export class DemoSource implements PositionSource {
  private timer: ReturnType<typeof setInterval> | null = null
  private t = 0

  constructor(
    private readonly centerLat = 45.764,
    private readonly centerLon = 4.8357,
  ) {}

  start(onPoint: PositionCallback): void {
    this.timer = setInterval(() => {
      this.t += 1
      // Boucle elliptique d'environ 2,4 km parcourue à ~20 km/h.
      const angle = (this.t * 2 * Math.PI) / 450
      const lat = this.centerLat + 0.0035 * Math.sin(angle)
      const lon = this.centerLon + 0.005 * Math.cos(angle)
      onPoint({
        lat,
        lon,
        alt: 170 + 15 * Math.sin(angle * 2),
        time: Date.now(),
        accuracy: 5,
        speed: null,
      })
    }, 1000)
  }

  stop(): void {
    if (this.timer != null) {
      clearInterval(this.timer)
      this.timer = null
    }
  }
}
