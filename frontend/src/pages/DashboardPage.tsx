import { Link } from 'react-router-dom'
import type { Account, Invoice, Product, RegisterSession, RepairJob } from '../api/types'
import { money, time } from '../lib/format'
import { useApi } from '../lib/useApi'

export default function DashboardPage() {
  const lowStock = useApi<Product[]>('/api/inventory/low-stock')
  const planning = useApi<RepairJob[]>('/api/workshop/planning')
  const invoices = useApi<Invoice[]>('/api/invoicing/invoices')
  const balance = useApi<Account[]>('/api/accounting/balance')
  const session = useApi<RegisterSession>('/api/pos/sessions/current')

  const openInvoices = (invoices.data ?? []).filter((i) => i.status === 'Issued')
  const cash = (balance.data ?? []).find((a) => a.number === '530')?.balance ?? 0
  const bank = (balance.data ?? []).find((a) => a.number === '512')?.balance ?? 0
  const todayJobs = (planning.data ?? []).filter(
    (j) => j.start.slice(0, 10) === new Date().toISOString().slice(0, 10),
  )

  return (
    <div>
      <h2>Tableau de bord</h2>

      <div className="grid grid-3">
        <div className="card stat">
          <div className="value">{session.data && !session.error ? '🟢 Ouverte' : '🔴 Fermée'}</div>
          <div className="label">
            <Link to="/caisse">Caisse</Link>
          </div>
        </div>
        <div className="card stat">
          <div className="value">{money(cash + bank)}</div>
          <div className="label">Trésorerie (caisse + banque)</div>
        </div>
        <div className="card stat">
          <div className="value">{openInvoices.length}</div>
          <div className="label">
            <Link to="/factures">Factures en attente de règlement</Link>
          </div>
        </div>
      </div>

      <div className="grid grid-2">
        <div className="card">
          <h3 style={{ marginTop: 0 }}>🔧 Atelier aujourd'hui</h3>
          {todayJobs.length === 0 && <p>Aucune intervention prévue aujourd'hui.</p>}
          {todayJobs.length > 0 && (
            <table>
              <tbody>
                {todayJobs.map((job) => (
                  <tr key={job.id}>
                    <td>
                      {time(job.start)}–{time(job.end)}
                    </td>
                    <td>{job.customerName}</td>
                    <td>{job.bikeDescription}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <p>
            <Link to="/atelier">Voir le planning complet →</Link>
          </p>
        </div>

        <div className="card">
          <h3 style={{ marginTop: 0 }}>📦 Stocks à réapprovisionner</h3>
          {(lowStock.data ?? []).length === 0 && <p>Tous les stocks sont au-dessus de leur seuil.</p>}
          {(lowStock.data ?? []).length > 0 && (
            <table>
              <tbody>
                {(lowStock.data ?? []).map((p) => (
                  <tr key={p.id}>
                    <td>{p.name}</td>
                    <td className="num">
                      <span className="badge red">
                        {p.quantityOnHand} / seuil {p.reorderThreshold}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <p>
            <Link to="/approvisionnement">Passer commande →</Link>
          </p>
        </div>
      </div>
    </div>
  )
}
