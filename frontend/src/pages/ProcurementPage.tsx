import { useState } from 'react'
import { apiPost } from '../api/client'
import type {
  Product,
  PurchaseOrder,
  PurchaseOrderStatus,
  ReplenishmentSuggestion,
  Supplier,
} from '../api/types'
import { dateTime, money } from '../lib/format'
import { useApi } from '../lib/useApi'

const statusLabels: Record<PurchaseOrderStatus, { label: string; color: string }> = {
  Draft: { label: 'Brouillon', color: '' },
  Ordered: { label: 'Commandée', color: 'blue' },
  Received: { label: 'Réceptionnée', color: 'green' },
  Cancelled: { label: 'Annulée', color: 'red' },
}

interface OrderLineDraft {
  productId: string
  quantity: string
}

export default function ProcurementPage() {
  const suppliers = useApi<Supplier[]>('/api/procurement/suppliers')
  const orders = useApi<PurchaseOrder[]>('/api/procurement/orders')
  const suggestions = useApi<ReplenishmentSuggestion[]>('/api/procurement/replenishment-suggestions')
  const products = useApi<Product[]>('/api/inventory/products')
  const [error, setError] = useState<string | null>(null)
  const [supplierForm, setSupplierForm] = useState({ name: '', email: '', phone: '' })
  const [orderSupplierId, setOrderSupplierId] = useState('')
  const [orderLines, setOrderLines] = useState<OrderLineDraft[]>([{ productId: '', quantity: '1' }])

  const supplierNames = new Map((suppliers.data ?? []).map((s) => [s.id, s.name]))

  const run = async (action: () => Promise<unknown>) => {
    setError(null)
    try {
      await action()
      orders.reload()
      suggestions.reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const createSupplier = (e: React.FormEvent) => {
    e.preventDefault()
    run(async () => {
      await apiPost('/api/procurement/suppliers', {
        name: supplierForm.name,
        email: supplierForm.email || null,
        phone: supplierForm.phone || null,
      })
      setSupplierForm({ name: '', email: '', phone: '' })
      suppliers.reload()
    })
  }

  const createOrder = (e: React.FormEvent) => {
    e.preventDefault()
    run(async () => {
      await apiPost('/api/procurement/orders', {
        supplierId: orderSupplierId,
        lines: orderLines
          .filter((l) => l.productId && Number(l.quantity) > 0)
          .map((l) => ({ productId: l.productId, quantity: Number(l.quantity) })),
      })
      setOrderLines([{ productId: '', quantity: '1' }])
    })
  }

  // Pré-remplit la commande depuis une suggestion de réassort.
  const orderFromSuggestion = (suggestion: ReplenishmentSuggestion) => {
    if (suggestion.preferredSupplierId) setOrderSupplierId(suggestion.preferredSupplierId)
    setOrderLines([{ productId: suggestion.id, quantity: String(suggestion.suggestedQuantity) }])
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  return (
    <div>
      <h2>Approvisionnement</h2>
      {error && <div className="error">{error}</div>}

      <div className="grid grid-2">
        <div className="card">
          <h3 style={{ marginTop: 0 }}>Nouvelle commande fournisseur</h3>
          <form onSubmit={createOrder}>
            <label style={{ display: 'block', maxWidth: '20rem', marginBottom: '0.6rem' }}>
              Fournisseur
              <select required value={orderSupplierId} onChange={(e) => setOrderSupplierId(e.target.value)}>
                <option value="">Choisir…</option>
                {(suppliers.data ?? []).map((s) => (
                  <option key={s.id} value={s.id}>{s.name}</option>
                ))}
              </select>
            </label>
            {orderLines.map((line, index) => (
              <div key={index} style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.4rem' }}>
                <select
                  value={line.productId}
                  onChange={(e) =>
                    setOrderLines((ls) => ls.map((l, i) => (i === index ? { ...l, productId: e.target.value } : l)))
                  }
                >
                  <option value="">Choisir un composant…</option>
                  {(products.data ?? []).map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} (coût {money(p.unitCost)})
                    </option>
                  ))}
                </select>
                <input
                  type="number"
                  min="1"
                  style={{ width: '5rem' }}
                  value={line.quantity}
                  onChange={(e) =>
                    setOrderLines((ls) => ls.map((l, i) => (i === index ? { ...l, quantity: e.target.value } : l)))
                  }
                />
                <button
                  type="button"
                  className="danger small"
                  onClick={() => setOrderLines((ls) => ls.filter((_, i) => i !== index))}
                >
                  ✕
                </button>
              </div>
            ))}
            <button
              type="button"
              className="secondary small"
              onClick={() => setOrderLines((ls) => [...ls, { productId: '', quantity: '1' }])}
            >
              + Ajouter une ligne
            </button>
            <div style={{ marginTop: '0.8rem' }}>
              <button type="submit">Créer la commande</button>
            </div>
          </form>
        </div>

        <div className="card">
          <h3 style={{ marginTop: 0 }}>Fournisseurs</h3>
          <ul>
            {(suppliers.data ?? []).map((s) => (
              <li key={s.id}>
                {s.name}
                {s.email ? ` — ${s.email}` : ''} (délai {s.leadTimeDays} j)
              </li>
            ))}
          </ul>
          <form className="inline" onSubmit={createSupplier}>
            <label>
              Nom
              <input
                required
                value={supplierForm.name}
                onChange={(e) => setSupplierForm((f) => ({ ...f, name: e.target.value }))}
              />
            </label>
            <label>
              Email
              <input
                type="email"
                value={supplierForm.email}
                onChange={(e) => setSupplierForm((f) => ({ ...f, email: e.target.value }))}
              />
            </label>
            <button type="submit">Ajouter</button>
          </form>
        </div>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>Suggestions de réassort (stock sous le seuil)</h3>
        {(suggestions.data ?? []).length === 0 && <p>Aucun produit sous son seuil de réassort. 👍</p>}
        {(suggestions.data ?? []).length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Réf.</th>
                <th>Produit</th>
                <th className="num">Stock</th>
                <th className="num">Seuil</th>
                <th className="num">Qté suggérée</th>
                <th>Fournisseur privilégié</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {(suggestions.data ?? []).map((s) => (
                <tr key={s.id}>
                  <td>{s.sku}</td>
                  <td>{s.name}</td>
                  <td className="num">{s.quantityOnHand}</td>
                  <td className="num">{s.reorderThreshold}</td>
                  <td className="num">{s.suggestedQuantity}</td>
                  <td>{s.preferredSupplierName ?? '—'}</td>
                  <td>
                    <button className="small" onClick={() => orderFromSuggestion(s)}>
                      Commander
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>Commandes</h3>
        <table>
          <thead>
            <tr>
              <th>Numéro</th>
              <th>Fournisseur</th>
              <th>Créée le</th>
              <th className="num">Total TTC</th>
              <th>Statut</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {(orders.data ?? []).map((order) => {
              const status = statusLabels[order.status]
              return (
                <tr key={order.id}>
                  <td>{order.number}</td>
                  <td>{supplierNames.get(order.supplierId) ?? '—'}</td>
                  <td>{dateTime(order.createdAt)}</td>
                  <td className="num">{money(order.totalInclVat)}</td>
                  <td>
                    <span className={`badge ${status.color}`}>{status.label}</span>
                  </td>
                  <td style={{ display: 'flex', gap: '0.3rem' }}>
                    {order.status === 'Draft' && (
                      <button
                        className="small"
                        onClick={() => run(() => apiPost(`/api/procurement/orders/${order.id}/send`))}
                      >
                        Envoyer
                      </button>
                    )}
                    {order.status === 'Ordered' && (
                      <button
                        className="small"
                        onClick={() => run(() => apiPost(`/api/procurement/orders/${order.id}/receive`))}
                      >
                        Réceptionner
                      </button>
                    )}
                    {(order.status === 'Draft' || order.status === 'Ordered') && (
                      <button
                        className="danger small"
                        onClick={() => run(() => apiPost(`/api/procurement/orders/${order.id}/cancel`))}
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
    </div>
  )
}
