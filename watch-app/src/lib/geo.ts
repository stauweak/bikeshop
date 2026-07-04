export interface TrackPoint {
  lat: number
  lon: number
  /** Altitude en mètres, si disponible */
  alt: number | null
  /** Horodatage en ms epoch */
  time: number
  /** Précision horizontale en mètres */
  accuracy: number
  /** Vitesse fournie par le capteur en m/s, si disponible */
  speed: number | null
}

const EARTH_RADIUS_M = 6371000

/** Distance en mètres entre deux points (formule de haversine). */
export function haversine(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const toRad = (d: number) => (d * Math.PI) / 180
  const dLat = toRad(lat2 - lat1)
  const dLon = toRad(lon2 - lon1)
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2
  return 2 * EARTH_RADIUS_M * Math.asin(Math.sqrt(a))
}

/** Vitesse en m/s entre deux points consécutifs. */
export function speedBetween(a: TrackPoint, b: TrackPoint): number {
  const dt = (b.time - a.time) / 1000
  if (dt <= 0) return 0
  return haversine(a.lat, a.lon, b.lat, b.lon) / dt
}

export function formatDuration(ms: number): string {
  const totalSec = Math.floor(ms / 1000)
  const h = Math.floor(totalSec / 3600)
  const m = Math.floor((totalSec % 3600) / 60)
  const s = totalSec % 60
  const pad = (n: number) => String(n).padStart(2, '0')
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${m}:${pad(s)}`
}

export function formatDistance(meters: number): string {
  return meters >= 1000 ? `${(meters / 1000).toFixed(2)} km` : `${Math.round(meters)} m`
}

/** m/s → km/h formaté. */
export function formatSpeed(ms: number): string {
  return `${(ms * 3.6).toFixed(1)}`
}

/** Allure en min/km à partir d'une vitesse en m/s. */
export function formatPace(ms: number): string {
  if (ms < 0.3) return '--:--'
  const secPerKm = 1000 / ms
  const m = Math.floor(secPerKm / 60)
  const s = Math.round(secPerKm % 60)
  if (m > 99) return '--:--'
  return `${m}:${String(s).padStart(2, '0')}`
}

/** Génère un fichier GPX 1.1 à partir des points enregistrés. */
export function toGpx(points: TrackPoint[], name: string): string {
  const trkpts = points
    .map((p) => {
      const ele = p.alt != null ? `<ele>${p.alt.toFixed(1)}</ele>` : ''
      return `      <trkpt lat="${p.lat.toFixed(7)}" lon="${p.lon.toFixed(7)}">${ele}<time>${new Date(p.time).toISOString()}</time></trkpt>`
    })
    .join('\n')
  return `<?xml version="1.0" encoding="UTF-8"?>
<gpx version="1.1" creator="SportTrack" xmlns="http://www.topografix.com/GPX/1/1">
  <trk>
    <name>${name}</name>
    <trkseg>
${trkpts}
    </trkseg>
  </trk>
</gpx>
`
}
