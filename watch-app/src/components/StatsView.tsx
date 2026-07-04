import {
  formatDistance,
  formatDuration,
  formatPace,
  formatSpeed,
} from '../lib/geo'
import type { SessionStats } from '../lib/session'

interface Props {
  stats: SessionStats
}

export function StatsView({ stats }: Props) {
  return (
    <div className="stats">
      <div className="stat stat-main">
        <span className="stat-value" data-testid="speed">{formatSpeed(stats.speed)}</span>
        <span className="stat-label">km/h</span>
      </div>
      <div className="stat-row">
        <div className="stat">
          <span className="stat-value" data-testid="distance">{formatDistance(stats.distance)}</span>
          <span className="stat-label">distance</span>
        </div>
        <div className="stat">
          <span className="stat-value" data-testid="duration">{formatDuration(stats.elapsed)}</span>
          <span className="stat-label">durée</span>
        </div>
      </div>
      <div className="stat-row">
        <div className="stat">
          <span className="stat-value">{formatSpeed(stats.avgSpeed)}</span>
          <span className="stat-label">moy. km/h</span>
        </div>
        <div className="stat">
          <span className="stat-value">{formatSpeed(stats.maxSpeed)}</span>
          <span className="stat-label">max km/h</span>
        </div>
      </div>
      <div className="stat-row">
        <div className="stat">
          <span className="stat-value">{formatPace(stats.avgSpeed)}</span>
          <span className="stat-label">min/km</span>
        </div>
        <div className="stat">
          <span className="stat-value">
            {stats.altitude != null ? `${Math.round(stats.altitude)} m` : '--'}
          </span>
          <span className="stat-label">altitude</span>
        </div>
        <div className="stat">
          <span className="stat-value">{`+${Math.round(stats.elevationGain)} m`}</span>
          <span className="stat-label">D+</span>
        </div>
      </div>
    </div>
  )
}
