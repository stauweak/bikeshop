import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { TrackPoint } from '../lib/geo'

interface Props {
  points: TrackPoint[]
  position: TrackPoint | null
  /** Suit automatiquement la position courante */
  follow: boolean
}

export function MapView({ points, position, follow }: Props) {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<L.Map | null>(null)
  const trackRef = useRef<L.Polyline | null>(null)
  const markerRef = useRef<L.CircleMarker | null>(null)

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return
    const map = L.map(containerRef.current, {
      zoomControl: false,
      attributionControl: false,
      center: [45.764, 4.8357],
      zoom: 16,
    })
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '© OpenStreetMap',
    }).addTo(map)
    L.control.attribution({ position: 'bottomright', prefix: false }).addTo(map)
    trackRef.current = L.polyline([], { color: '#22d3ee', weight: 4, opacity: 0.9 }).addTo(map)
    markerRef.current = L.circleMarker([45.764, 4.8357], {
      radius: 7,
      color: '#0b0f14',
      weight: 2,
      fillColor: '#22d3ee',
      fillOpacity: 1,
    })
    mapRef.current = map
    return () => {
      map.remove()
      mapRef.current = null
      trackRef.current = null
      markerRef.current = null
    }
  }, [])

  // Met à jour le tracé du parcours.
  useEffect(() => {
    trackRef.current?.setLatLngs(points.map((p) => [p.lat, p.lon]))
  }, [points, points.length])

  // Met à jour le marqueur de position et recentre la carte.
  useEffect(() => {
    const map = mapRef.current
    const marker = markerRef.current
    if (!map || !marker || !position) return
    const latlng: L.LatLngExpression = [position.lat, position.lon]
    marker.setLatLng(latlng)
    if (!map.hasLayer(marker)) marker.addTo(map)
    if (follow) map.panTo(latlng, { animate: true })
  }, [position, follow])

  // Leaflet a besoin d'un recalcul de taille quand la vue devient visible.
  useEffect(() => {
    const map = mapRef.current
    if (!map || !containerRef.current) return
    const observer = new ResizeObserver(() => map.invalidateSize())
    observer.observe(containerRef.current)
    return () => observer.disconnect()
  }, [])

  return <div ref={containerRef} className="map" />
}
