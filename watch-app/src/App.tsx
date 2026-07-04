import { useState } from 'react'
import { MapView } from './components/MapView'
import { StatsView } from './components/StatsView'
import { useTracker } from './hooks/useTracker'
import { toGpx } from './lib/geo'

type View = 'stats' | 'map'

function downloadGpx(gpx: string) {
  const blob = new Blob([gpx], { type: 'application/gpx+xml' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `sporttrack-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.gpx`
  a.click()
  URL.revokeObjectURL(url)
}

export default function App() {
  const tracker = useTracker()
  const [view, setView] = useState<View>('stats')

  const exportGpx = () => {
    if (tracker.points.length === 0) return
    downloadGpx(toGpx(tracker.points, `Séance ${new Date().toLocaleString('fr-FR')}`))
  }

  return (
    <div className="watch">
      <div className="screen">
        {view === 'map' ? (
          <MapView points={tracker.points} position={tracker.position} follow />
        ) : (
          <StatsView stats={tracker.stats} />
        )}

        {tracker.error && <div className="error">{tracker.error}</div>}

        <header className="topbar">
          <button
            className={`chip ${tracker.demoMode ? 'chip-on' : ''}`}
            onClick={tracker.toggleDemoMode}
            disabled={tracker.state === 'running'}
            title="Simule un parcours sans capteur GPS"
          >
            démo
          </button>
          <span className={`gps-dot ${tracker.position ? 'gps-ok' : ''}`} title="Signal GPS" />
        </header>

        <nav className="tabs">
          <button
            className={view === 'stats' ? 'tab tab-active' : 'tab'}
            onClick={() => setView('stats')}
          >
            Données
          </button>
          <button
            className={view === 'map' ? 'tab tab-active' : 'tab'}
            onClick={() => setView('map')}
          >
            Carte
          </button>
        </nav>

        <footer className="controls">
          {tracker.state === 'idle' && (
            <button className="btn btn-start" onClick={tracker.start}>
              Démarrer
            </button>
          )}
          {tracker.state === 'running' && (
            <button className="btn btn-pause" onClick={tracker.pause}>
              Pause
            </button>
          )}
          {tracker.state === 'paused' && (
            <>
              <button className="btn btn-start" onClick={tracker.resume}>
                Reprendre
              </button>
              <button className="btn btn-stop" onClick={exportGpx} disabled={tracker.points.length === 0}>
                GPX
              </button>
              <button className="btn btn-stop" onClick={tracker.reset}>
                Terminer
              </button>
            </>
          )}
        </footer>
      </div>
    </div>
  )
}
