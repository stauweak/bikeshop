import { useState } from 'react'
import { apiPost } from '../api/client'
import type { Product, RegisterSession, Sale, SessionSummary } from '../api/types'
import { money } from '../lib/format'
import { useApi } from '../lib/useApi'

interface CartLine {
  product: Product
  quantity: number
}

export default function PosPage() {
  const session = useApi<RegisterSession>('/api/pos/sessions/current')
  const products = useApi<Product[]>('/api/inventory/products')
  const [cart, setCart] = useState<CartLine[]>([])
  const [error, setError] = useState<string | null>(null)
  const [lastSale, setLastSale] = useState<Sale | null>(null)
  const [summary, setSummary] = useState<SessionSummary | null>(null)
  const [openingFloat, setOpeningFloat] = useState('100')
  const [countedCash, setCountedCash] = useState('')

  const sessionOpen = session.data !== null && !session.error

  const addToCart = (product: Product) =>
    setCart((lines) => {
      const existing = lines.find((l) => l.product.id === product.id)
      return existing
        ? lines.map((l) => (l.product.id === product.id ? { ...l, quantity: l.quantity + 1 } : l))
        : [...lines, { product, quantity: 1 }]
    })

  const removeFromCart = (productId: string) =>
    setCart((lines) => lines.filter((l) => l.product.id !== productId))

  const totalInclVat = cart.reduce(
    (sum, l) => sum + l.quantity * l.product.unitPrice * (1 + l.product.vatRate),
    0,
  )

  const openSession = async () => {
    setError(null)
    try {
      await apiPost('/api/pos/sessions/open', { openingFloat: Number(openingFloat) })
      setSummary(null)
      session.reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const closeSession = async () => {
    if (!session.data) return
    setError(null)
    try {
      const result = await apiPost<SessionSummary>(
        `/api/pos/sessions/${session.data.id}/close`,
        { countedCash: Number(countedCash) },
      )
      setSummary(result)
      setCart([])
      setLastSale(null)
      session.reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const checkout = async (paymentMethod: 'Cash' | 'Card') => {
    setError(null)
    try {
      const sale = await apiPost<Sale>('/api/pos/sales', {
        paymentMethod,
        lines: cart.map((l) => ({ productId: l.product.id, quantity: l.quantity })),
      })
      setLastSale(sale)
      setCart([])
      products.reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  return (
    <div>
      <h2>Caisse</h2>
      {error && <div className="error">{error}</div>}

      {!sessionOpen && (
        <div className="card">
          <h3>Ouvrir la caisse</h3>
          <form
            className="inline"
            onSubmit={(e) => {
              e.preventDefault()
              openSession()
            }}
          >
            <label>
              Fond de caisse (€)
              <input
                type="number"
                step="0.01"
                value={openingFloat}
                onChange={(e) => setOpeningFloat(e.target.value)}
              />
            </label>
            <button type="submit">Ouvrir la session</button>
          </form>
          {summary && <ZReport summary={summary} />}
        </div>
      )}

      {sessionOpen && session.data && (
        <>
          <div className="card">
            Session ouverte depuis le{' '}
            {new Date(session.data.openedAt).toLocaleString('fr-FR')} — fond de caisse{' '}
            {money(session.data.openingFloat)}
          </div>

          <div className="pos-grid">
            <div>
              <div className="product-tiles">
                {(products.data ?? [])
                  .filter((p) => p.quantityOnHand > 0)
                  .map((p) => (
                    <button key={p.id} className="product-tile" onClick={() => addToCart(p)}>
                      <div>{p.name}</div>
                      <div className="price">{money(p.unitPrice * (1 + p.vatRate))}</div>
                      <div className="stock">Stock : {p.quantityOnHand}</div>
                    </button>
                  ))}
              </div>
            </div>

            <div className="card">
              <h3 style={{ marginTop: 0 }}>Ticket</h3>
              {cart.length === 0 && <p>Cliquez sur un article pour l'ajouter.</p>}
              {cart.length > 0 && (
                <table>
                  <tbody>
                    {cart.map((l) => (
                      <tr key={l.product.id}>
                        <td>
                          {l.quantity} × {l.product.name}
                        </td>
                        <td className="num">
                          {money(l.quantity * l.product.unitPrice * (1 + l.product.vatRate))}
                        </td>
                        <td>
                          <button
                            className="danger small"
                            onClick={() => removeFromCart(l.product.id)}
                          >
                            ✕
                          </button>
                        </td>
                      </tr>
                    ))}
                    <tr>
                      <th>Total TTC</th>
                      <th className="num">{money(totalInclVat)}</th>
                      <th />
                    </tr>
                  </tbody>
                </table>
              )}
              <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.8rem' }}>
                <button disabled={cart.length === 0} onClick={() => checkout('Cash')}>
                  💶 Espèces
                </button>
                <button disabled={cart.length === 0} onClick={() => checkout('Card')}>
                  💳 Carte
                </button>
              </div>
              {lastSale && (
                <p>
                  ✅ Ticket {lastSale.number} encaissé : {money(lastSale.totalInclVat)} (
                  {lastSale.paymentMethod === 'Cash' ? 'espèces' : 'carte'})
                </p>
              )}
            </div>
          </div>

          <div className="card">
            <h3 style={{ marginTop: 0 }}>Clôturer la caisse</h3>
            <form
              className="inline"
              onSubmit={(e) => {
                e.preventDefault()
                closeSession()
              }}
            >
              <label>
                Espèces comptées (€)
                <input
                  type="number"
                  step="0.01"
                  required
                  value={countedCash}
                  onChange={(e) => setCountedCash(e.target.value)}
                />
              </label>
              <button type="submit" className="danger">
                Clôturer la session
              </button>
            </form>
          </div>
        </>
      )}
    </div>
  )
}

function ZReport({ summary }: { summary: SessionSummary }) {
  return (
    <div>
      <h3>Ticket Z — session clôturée</h3>
      <table>
        <tbody>
          <tr><td>Nombre de ventes</td><td className="num">{summary.saleCount}</td></tr>
          <tr><td>Total espèces</td><td className="num">{money(summary.cashTotal)}</td></tr>
          <tr><td>Total carte</td><td className="num">{money(summary.cardTotal)}</td></tr>
          <tr><td>Chiffre d'affaires TTC</td><td className="num">{money(summary.total)}</td></tr>
          <tr><td>Espèces attendues</td><td className="num">{money(summary.expectedCash)}</td></tr>
          <tr><td>Espèces comptées</td><td className="num">{summary.countedCash !== null ? money(summary.countedCash) : '—'}</td></tr>
          <tr>
            <th>Écart de caisse</th>
            <th className="num">{summary.cashDifference !== null ? money(summary.cashDifference) : '—'}</th>
          </tr>
        </tbody>
      </table>
    </div>
  )
}
