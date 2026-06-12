import { useState } from 'react'
import { apiPost } from '../api/client'
import type { Product, StockMovement, Supplier } from '../api/types'
import { dateTime, money } from '../lib/format'
import { useApi } from '../lib/useApi'

const emptyForm = {
  sku: '',
  name: '',
  category: 'Component',
  unitPrice: '0',
  unitCost: '0',
  vatRate: '0.20',
  quantityOnHand: '0',
  reorderThreshold: '0',
  preferredSupplierId: '',
}

const categoryLabels: Record<string, string> = {
  Bike: 'Vélo',
  Component: 'Composant',
  Accessory: 'Accessoire',
  Consumable: 'Consommable',
}

const reasonLabels: Record<string, string> = {
  Sale: 'Vente caisse',
  PurchaseReception: 'Réception fournisseur',
  WorkshopUse: 'Utilisation atelier',
  Adjustment: 'Ajustement',
}

export default function InventoryPage() {
  const products = useApi<Product[]>('/api/inventory/products')
  const movements = useApi<StockMovement[]>('/api/inventory/movements')
  const suppliers = useApi<Supplier[]>('/api/procurement/suppliers')
  const [form, setForm] = useState(emptyForm)
  const [error, setError] = useState<string | null>(null)

  const productNames = new Map((products.data ?? []).map((p) => [p.id, p.name]))

  const set = (field: keyof typeof emptyForm) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }))

  const createProduct = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      await apiPost('/api/inventory/products', {
        sku: form.sku,
        name: form.name,
        category: form.category,
        unitPrice: Number(form.unitPrice),
        unitCost: Number(form.unitCost),
        vatRate: Number(form.vatRate),
        quantityOnHand: Number(form.quantityOnHand),
        reorderThreshold: Number(form.reorderThreshold),
        preferredSupplierId: form.preferredSupplierId || null,
      })
      setForm(emptyForm)
      products.reload()
      movements.reload()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const adjust = async (product: Product) => {
    const input = window.prompt(
      `Ajustement de stock pour « ${product.name} » (quantité, négatif pour une sortie) :`,
    )
    if (!input) return
    setError(null)
    try {
      await apiPost(`/api/inventory/products/${product.id}/adjust`, {
        quantity: Number(input),
        reason: 'Adjustment',
        reference: 'Ajustement manuel',
      })
      products.reload()
      movements.reload()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <div>
      <h2>Stocks</h2>
      {error && <div className="error">{error}</div>}

      <div className="card">
        <h3 style={{ marginTop: 0 }}>Nouveau produit</h3>
        <form className="inline" onSubmit={createProduct}>
          <label>
            Référence
            <input required value={form.sku} onChange={set('sku')} />
          </label>
          <label>
            Nom
            <input required value={form.name} onChange={set('name')} />
          </label>
          <label>
            Catégorie
            <select value={form.category} onChange={set('category')}>
              {Object.entries(categoryLabels).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </label>
          <label>
            Prix vente HT
            <input type="number" step="0.01" value={form.unitPrice} onChange={set('unitPrice')} />
          </label>
          <label>
            Coût achat HT
            <input type="number" step="0.01" value={form.unitCost} onChange={set('unitCost')} />
          </label>
          <label>
            Stock initial
            <input type="number" step="1" value={form.quantityOnHand} onChange={set('quantityOnHand')} />
          </label>
          <label>
            Seuil de réassort
            <input type="number" step="1" value={form.reorderThreshold} onChange={set('reorderThreshold')} />
          </label>
          <label>
            Fournisseur privilégié
            <select value={form.preferredSupplierId} onChange={set('preferredSupplierId')}>
              <option value="">—</option>
              {(suppliers.data ?? []).map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
          </label>
          <button type="submit">Créer</button>
        </form>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>Catalogue</h3>
        <table>
          <thead>
            <tr>
              <th>Réf.</th>
              <th>Nom</th>
              <th>Catégorie</th>
              <th className="num">Prix HT</th>
              <th className="num">Stock</th>
              <th className="num">Seuil</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {(products.data ?? []).map((p) => (
              <tr key={p.id}>
                <td>{p.sku}</td>
                <td>{p.name}</td>
                <td>{categoryLabels[p.category] ?? p.category}</td>
                <td className="num">{money(p.unitPrice)}</td>
                <td className="num">
                  {p.quantityOnHand <= p.reorderThreshold ? (
                    <span className="badge red">{p.quantityOnHand}</span>
                  ) : (
                    p.quantityOnHand
                  )}
                </td>
                <td className="num">{p.reorderThreshold}</td>
                <td>
                  <button className="secondary small" onClick={() => adjust(p)}>
                    Ajuster
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>Derniers mouvements</h3>
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Produit</th>
              <th className="num">Quantité</th>
              <th>Motif</th>
              <th>Référence</th>
            </tr>
          </thead>
          <tbody>
            {(movements.data ?? []).slice(0, 30).map((m) => (
              <tr key={m.id}>
                <td>{dateTime(m.occurredAt)}</td>
                <td>{productNames.get(m.productId) ?? m.productId}</td>
                <td className="num">{m.quantity > 0 ? `+${m.quantity}` : m.quantity}</td>
                <td>{reasonLabels[m.reason] ?? m.reason}</td>
                <td>{m.reference}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
