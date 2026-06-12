import { useState } from 'react'
import { apiPost } from '../api/client'
import type { Mechanic, Product, RepairJob, RepairJobStatus } from '../api/types'
import { date, money, time } from '../lib/format'
import { useApi } from '../lib/useApi'

const statusLabels: Record<RepairJobStatus, { label: string; color: string }> = {
  Scheduled: { label: 'Planifiée', color: 'blue' },
  InProgress: { label: 'En cours', color: 'orange' },
  Completed: { label: 'Terminée', color: 'green' },
  Invoiced: { label: 'Facturée', color: '' },
  Cancelled: { label: 'Annulée', color: 'red' },
}

interface PartDraft {
  productId: string
  quantity: string
}

export default function WorkshopPage() {
  const jobs = useApi<RepairJob[]>('/api/workshop/jobs')
  const mechanics = useApi<Mechanic[]>('/api/workshop/mechanics')
  const products = useApi<Product[]>('/api/inventory/products')
  const [error, setError] = useState<string | null>(null)
  const [mechanicName, setMechanicName] = useState('')
  const [form, setForm] = useState({
    customerName: '',
    customerPhone: '',
    bikeDescription: '',
    mechanicId: '',
    day: new Date().toISOString().slice(0, 10),
    startTime: '09:00',
    endTime: '10:00',
    laborHours: '1',
    notes: '',
  })
  const [parts, setParts] = useState<PartDraft[]>([])

  const mechanicNames = new Map((mechanics.data ?? []).map((m) => [m.id, m.name]))

  const run = async (action: () => Promise<unknown>) => {
    setError(null)
    try {
      await action()
      jobs.reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const addMechanic = (e: React.FormEvent) => {
    e.preventDefault()
    run(async () => {
      await apiPost('/api/workshop/mechanics', { name: mechanicName })
      setMechanicName('')
      mechanics.reload()
    })
  }

  const createJob = (e: React.FormEvent) => {
    e.preventDefault()
    run(async () => {
      await apiPost('/api/workshop/jobs', {
        customerName: form.customerName,
        customerPhone: form.customerPhone || null,
        bikeDescription: form.bikeDescription,
        mechanicId: form.mechanicId,
        start: `${form.day}T${form.startTime}:00`,
        end: `${form.day}T${form.endTime}:00`,
        laborHours: Number(form.laborHours),
        notes: form.notes || null,
        parts: parts
          .filter((p) => p.productId && Number(p.quantity) > 0)
          .map((p) => ({ productId: p.productId, quantity: Number(p.quantity) })),
      })
      setForm((f) => ({ ...f, customerName: '', customerPhone: '', bikeDescription: '', notes: '' }))
      setParts([])
    })
  }

  const set = (field: keyof typeof form) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
      setForm((f) => ({ ...f, [field]: e.target.value }))

  // Planning groupé par jour, interventions actives uniquement.
  const planning = (jobs.data ?? [])
    .filter((j) => j.status !== 'Cancelled')
    .sort((a, b) => a.start.localeCompare(b.start))
  const byDay = new Map<string, RepairJob[]>()
  for (const job of planning) {
    const day = job.start.slice(0, 10)
    byDay.set(day, [...(byDay.get(day) ?? []), job])
  }

  return (
    <div>
      <h2>Atelier</h2>
      {error && <div className="error">{error}</div>}

      <div className="grid grid-2">
        <div className="card">
          <h3 style={{ marginTop: 0 }}>Nouvelle intervention</h3>
          <form onSubmit={createJob}>
            <div className="inline" style={{ display: 'flex', flexWrap: 'wrap', gap: '0.6rem' }}>
              <label>
                Client
                <input required value={form.customerName} onChange={set('customerName')} />
              </label>
              <label>
                Téléphone
                <input value={form.customerPhone} onChange={set('customerPhone')} />
              </label>
              <label>
                Vélo
                <input required value={form.bikeDescription} onChange={set('bikeDescription')} />
              </label>
              <label>
                Mécanicien
                <select required value={form.mechanicId} onChange={set('mechanicId')}>
                  <option value="">Choisir…</option>
                  {(mechanics.data ?? []).map((m) => (
                    <option key={m.id} value={m.id}>{m.name}</option>
                  ))}
                </select>
              </label>
              <label>
                Jour
                <input type="date" value={form.day} onChange={set('day')} />
              </label>
              <label>
                Début
                <input type="time" value={form.startTime} onChange={set('startTime')} />
              </label>
              <label>
                Fin
                <input type="time" value={form.endTime} onChange={set('endTime')} />
              </label>
              <label>
                Heures de main-d'œuvre
                <input type="number" step="0.25" value={form.laborHours} onChange={set('laborHours')} />
              </label>
            </div>

            <h4>Pièces prévues</h4>
            {parts.map((part, index) => (
              <div key={index} style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.4rem' }}>
                <select
                  value={part.productId}
                  onChange={(e) =>
                    setParts((ps) => ps.map((p, i) => (i === index ? { ...p, productId: e.target.value } : p)))
                  }
                >
                  <option value="">Choisir une pièce…</option>
                  {(products.data ?? []).map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} ({money(p.unitPrice)} HT)
                    </option>
                  ))}
                </select>
                <input
                  type="number"
                  step="1"
                  min="1"
                  style={{ width: '5rem' }}
                  value={part.quantity}
                  onChange={(e) =>
                    setParts((ps) => ps.map((p, i) => (i === index ? { ...p, quantity: e.target.value } : p)))
                  }
                />
                <button
                  type="button"
                  className="danger small"
                  onClick={() => setParts((ps) => ps.filter((_, i) => i !== index))}
                >
                  ✕
                </button>
              </div>
            ))}
            <button
              type="button"
              className="secondary small"
              onClick={() => setParts((ps) => [...ps, { productId: '', quantity: '1' }])}
            >
              + Ajouter une pièce
            </button>
            <div style={{ marginTop: '0.8rem' }}>
              <button type="submit">Planifier</button>
            </div>
          </form>
        </div>

        <div className="card">
          <h3 style={{ marginTop: 0 }}>Mécaniciens</h3>
          <ul>
            {(mechanics.data ?? []).map((m) => (
              <li key={m.id}>{m.name}</li>
            ))}
          </ul>
          <form className="inline" onSubmit={addMechanic}>
            <label>
              Nom
              <input required value={mechanicName} onChange={(e) => setMechanicName(e.target.value)} />
            </label>
            <button type="submit">Ajouter</button>
          </form>
        </div>
      </div>

      <h3>Planning</h3>
      {[...byDay.entries()].map(([day, dayJobs]) => (
        <div className="card" key={day}>
          <strong>{date(day)}</strong>
          <table>
            <thead>
              <tr>
                <th>Créneau</th>
                <th>Client</th>
                <th>Vélo</th>
                <th>Mécanicien</th>
                <th>Statut</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {dayJobs.map((job) => {
                const status = statusLabels[job.status]
                return (
                  <tr key={job.id}>
                    <td>
                      {time(job.start)}–{time(job.end)}
                    </td>
                    <td>{job.customerName}</td>
                    <td>{job.bikeDescription}</td>
                    <td>{mechanicNames.get(job.mechanicId) ?? '—'}</td>
                    <td>
                      <span className={`badge ${status.color}`}>{status.label}</span>
                    </td>
                    <td style={{ display: 'flex', gap: '0.3rem' }}>
                      {job.status === 'Scheduled' && (
                        <button
                          className="small"
                          onClick={() => run(() => apiPost(`/api/workshop/jobs/${job.id}/start`))}
                        >
                          Démarrer
                        </button>
                      )}
                      {(job.status === 'Scheduled' || job.status === 'InProgress') && (
                        <button
                          className="small"
                          onClick={() => run(() => apiPost(`/api/workshop/jobs/${job.id}/complete`))}
                        >
                          Clôturer
                        </button>
                      )}
                      {job.status === 'Completed' && (
                        <button
                          className="small"
                          onClick={() => run(() => apiPost(`/api/workshop/jobs/${job.id}/invoice`))}
                        >
                          Facturer
                        </button>
                      )}
                      {(job.status === 'Scheduled' || job.status === 'InProgress') && (
                        <button
                          className="danger small"
                          onClick={() => run(() => apiPost(`/api/workshop/jobs/${job.id}/cancel`))}
                        >
                          Annuler
                        </button>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      ))}
      {byDay.size === 0 && <p>Aucune intervention planifiée.</p>}
    </div>
  )
}
