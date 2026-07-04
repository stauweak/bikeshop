import { useCallback, useEffect, useRef, useState } from 'react'
import type { TrackPoint } from '../lib/geo'
import { EMPTY_STATS, TrackSession, type SessionStats } from '../lib/session'
import { DemoSource, GpsSource, type PositionSource } from '../lib/sources'

export type TrackerState = 'idle' | 'running' | 'paused'

export interface Tracker {
  state: TrackerState
  stats: SessionStats
  points: TrackPoint[]
  /** Dernière position connue, même hors enregistrement */
  position: TrackPoint | null
  error: string | null
  demoMode: boolean
  start(): void
  pause(): void
  resume(): void
  reset(): void
  toggleDemoMode(): void
}

export function useTracker(): Tracker {
  const sessionRef = useRef(new TrackSession())
  const sourceRef = useRef<PositionSource | null>(null)
  const [state, setState] = useState<TrackerState>('idle')
  const [stats, setStats] = useState<SessionStats>(EMPTY_STATS)
  const [position, setPosition] = useState<TrackPoint | null>(null)
  const [pointCount, setPointCount] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [demoMode, setDemoMode] = useState(false)

  const stopSource = useCallback(() => {
    sourceRef.current?.stop()
    sourceRef.current = null
  }, [])

  const startSource = useCallback(() => {
    stopSource()
    const source: PositionSource = demoMode ? new DemoSource() : new GpsSource()
    sourceRef.current = source
    source.start(
      (p) => {
        setError(null)
        setPosition(p)
        sessionRef.current.addPoint(p)
        setPointCount(sessionRef.current.points.length)
      },
      (msg) => setError(msg),
    )
  }, [demoMode, stopSource])

  const start = useCallback(() => {
    sessionRef.current = new TrackSession()
    sessionRef.current.start(Date.now())
    setPointCount(0)
    setError(null)
    setState('running')
    startSource()
  }, [startSource])

  const pause = useCallback(() => {
    sessionRef.current.pause(Date.now())
    stopSource()
    setState('paused')
  }, [stopSource])

  const resume = useCallback(() => {
    sessionRef.current.start(Date.now())
    setState('running')
    startSource()
  }, [startSource])

  const reset = useCallback(() => {
    stopSource()
    sessionRef.current = new TrackSession()
    setStats(EMPTY_STATS)
    setPointCount(0)
    setState('idle')
  }, [stopSource])

  const toggleDemoMode = useCallback(() => setDemoMode((d) => !d), [])

  // Rafraîchit le chrono et les statistiques une fois par seconde.
  useEffect(() => {
    if (state === 'idle') return
    const update = () => setStats(sessionRef.current.snapshot(Date.now()))
    update()
    const timer = setInterval(update, 1000)
    return () => clearInterval(timer)
  }, [state, pointCount])

  // Coupe la source de position au démontage.
  useEffect(() => stopSource, [stopSource])

  return {
    state,
    stats,
    points: sessionRef.current.points,
    position,
    error,
    demoMode,
    start,
    pause,
    resume,
    reset,
    toggleDemoMode,
  }
}
