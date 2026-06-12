import { useState } from 'react'
import { apiPost } from '../api/client'
import type { Invoice, InvoiceStatus } from '../api/types'
import { date, money } from '../lib/format'
import { useApi } from '../lib/useApi'

const statusLabels: Record<InvoiceStatus, { label: string; color: string }> = {
  Draft: { label: 'Brouillon', color: '' },
  Issued: { label: 'Émise', color: 'blue' },
  Paid: { label: 'Payée', color: 'green' },
  Cancelled: { label: 'Annulée', color: 'red' },
}

interface LineDraft {
  description: string
  quantity: string
  unitPrice: string
  isService: boolean
}

export default function InvoicingPage() {
  const invoices = useApi<Invoice[]>('/api/invoicing/invoices')
  const [error, setError] = useState<string | null>(null)
  const [customerName, setCustomerName] = useState('')
  const [lines, setLines] = useState<LineDraft[]>([
    { description: '', quantity: '1', unitPrice: '0', isService: true },
  ])
  const [expanded, setExpanded] = useState<string | null>(null)

  const run = async (action: () => Promise<unknown>) => {
    setError(null)
    try {
      await action()
      invoices.reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const createInvoice = (e: React.FormEvent) => {
    e.preventDefault()
    run(async () => {
      await apiPost('/api/invoicing/invoices', {
        customerName,
        lines: lines
          .filter((l) => l.description)
          .map((l) => ({
            description: l.description,
            quantity: Number(l.quantity),
            unitPrice: Number(l.unitPrice),
            vatRate: 0.2,
            isService: l.isService,
          })),
      })
      setCustomerName('')
      setLines([{ description: '', quantity: '1', unitPrice: '0', isService: true }])
    })
  }

  const updateLine = (index: number, patch: Partial<LineDraft>) =>
    setLines((ls) => ls.map((l, i) => (i === index ? { ...l, ...patch } : l)))

  const pay = (invoice: Invoice) => {
    const method = window.confirm(
      `Encaisser la facture ${invoice.number} (${money(invoice.totalInclVat)}).\nOK = virement/carte (banque), Annuler = espèces (caisse).`,
    )
      ? 'card'
      : 'cash'
    run(() => apiPost(`/api/invoicing/invoices/${invoice.id}/pay`, { paymentMethod: method }))
  }

  return (
    <div>
      <h2>Facturation</h2>
      {error && <div className="error">{error}</div>}

      <div className="card">
        <h3 style={{ marginTop: 0 }}>Nouvelle facture</h3>
        <form onSubmit={createInvoice}>
          <label style={{ display: 'block', maxWidth: '20rem', marginBottom: '0.6rem' }}>
            Client
            <input required value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
          </label>
          {lines.map((line, index) => (
            <div key={index} style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.4rem' }}>
              <input
                placeholder="Description"
                value={line.description}
                onChange={(e) => updateLine(index, { description: e.target.value })}
              />
              <input
                type="number"
                step="0.25"
                style={{ width: '6rem' }}
                title="Quantité"
                value={line.quantity}
                onChange={(e) => updateLine(index, { quantity: e.target.value })}
              />
              <input
                type="number"
                step="0.01"
                style={{ width: '8rem' }}
                title="Prix unitaire HT"
                value={line.unitPrice}
                onChange={(e) => updateLine(index, { unitPrice: e.target.value })}
              />
              <select
                style={{ width: '11rem' }}
                value={line.isService ? 'service' : 'goods'}
                onChange={(e) => updateLine(index, { isService: e.target.value === 'service' })}
              >
                <option value="service">Prestation</option>
                <option value="goods">Marchandise</option>
              </select>
              <button
                type="button"
                className="danger small"
                onClick={() => setLines((ls) => ls.filter((_, i) => i !== index))}
              >
                ✕
              </button>
            </div>
          ))}
          <button
            type="button"
            className="secondary small"
            onClick={() =>
              setLines((ls) => [...ls, { description: '', quantity: '1', unitPrice: '0', isService: true }])
            }
          >
            + Ajouter une ligne
          </button>
          <div style={{ marginTop: '0.8rem' }}>
            <button type="submit">Créer la facture</button>
          </div>
        </form>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>Factures</h3>
        <table>
          <thead>
            <tr>
              <th>Numéro</th>
              <th>Client</th>
              <th>Date</th>
              <th className="num">Total TTC</th>
              <th>Statut</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {(invoices.data ?? []).map((invoice) => {
              const status = statusLabels[invoice.status]
              return (
                <>
                  <tr key={invoice.id}>
                    <td>
                      <a
                        href="#"
                        onClick={(e) => {
                          e.preventDefault()
                          setExpanded(expanded === invoice.id ? null : invoice.id)
                        }}
                      >
                        {invoice.number}
                      </a>
                    </td>
                    <td>{invoice.customerName}</td>
                    <td>{date(invoice.issueDate)}</td>
                    <td className="num">{money(invoice.totalInclVat)}</td>
                    <td>
                      <span className={`badge ${status.color}`}>{status.label}</span>
                    </td>
                    <td style={{ display: 'flex', gap: '0.3rem' }}>
                      {invoice.status === 'Draft' && (
                        <button
                          className="small"
                          onClick={() => run(() => apiPost(`/api/invoicing/invoices/${invoice.id}/issue`))}
                        >
                          Émettre
                        </button>
                      )}
                      {invoice.status === 'Issued' && (
                        <button className="small" onClick={() => pay(invoice)}>
                          Encaisser
                        </button>
                      )}
                    </td>
                  </tr>
                  {expanded === invoice.id && (
                    <tr key={`${invoice.id}-detail`}>
                      <td colSpan={6}>
                        <table>
                          <thead>
                            <tr>
                              <th>Description</th>
                              <th className="num">Qté</th>
                              <th className="num">PU HT</th>
                              <th className="num">TVA</th>
                              <th className="num">Total HT</th>
                            </tr>
                          </thead>
                          <tbody>
                            {invoice.lines.map((line, i) => (
                              <tr key={i}>
                                <td>{line.description}</td>
                                <td className="num">{line.quantity}</td>
                                <td className="num">{money(line.unitPrice)}</td>
                                <td className="num">{(line.vatRate * 100).toFixed(0)} %</td>
                                <td className="num">{money(line.quantity * line.unitPrice)}</td>
                              </tr>
                            ))}
                            <tr>
                              <th colSpan={4}>Total HT / TVA / TTC</th>
                              <th className="num">
                                {money(invoice.totalExclVat)} / {money(invoice.totalVat)} /{' '}
                                {money(invoice.totalInclVat)}
                              </th>
                            </tr>
                          </tbody>
                        </table>
                      </td>
                    </tr>
                  )}
                </>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
