import { useState } from 'react'
import { apiGet, apiPost } from '../api/client'
import type { Account, JournalEntry, LedgerLine } from '../api/types'
import { date, money } from '../lib/format'
import { useApi } from '../lib/useApi'

const typeLabels: Record<string, string> = {
  Asset: 'Actif',
  Liability: 'Passif',
  Equity: 'Capitaux propres',
  Revenue: 'Produits',
  Expense: 'Charges',
}

interface EntryLineDraft {
  account: string
  debit: string
  credit: string
}

export default function AccountingPage() {
  const balance = useApi<Account[]>('/api/accounting/balance')
  const journal = useApi<JournalEntry[]>('/api/accounting/journal')
  const [error, setError] = useState<string | null>(null)
  const [ledger, setLedger] = useState<{ account: string; lines: LedgerLine[] } | null>(null)
  const [label, setLabel] = useState('')
  const [entryLines, setEntryLines] = useState<EntryLineDraft[]>([
    { account: '', debit: '', credit: '' },
    { account: '', debit: '', credit: '' },
  ])

  const accountNames = new Map((balance.data ?? []).map((a) => [a.number, a.name]))

  const showLedger = async (account: string) => {
    setError(null)
    try {
      const lines = await apiGet<LedgerLine[]>(`/api/accounting/accounts/${account}/ledger`)
      setLedger({ account, lines })
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const postEntry = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      await apiPost('/api/accounting/entries', {
        date: new Date().toISOString().slice(0, 10),
        label,
        lines: entryLines
          .filter((l) => l.account)
          .map((l) => ({
            account: l.account,
            debit: Number(l.debit) || 0,
            credit: Number(l.credit) || 0,
          })),
      })
      setLabel('')
      setEntryLines([
        { account: '', debit: '', credit: '' },
        { account: '', debit: '', credit: '' },
      ])
      balance.reload()
      journal.reload()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const updateLine = (index: number, patch: Partial<EntryLineDraft>) =>
    setEntryLines((ls) => ls.map((l, i) => (i === index ? { ...l, ...patch } : l)))

  return (
    <div>
      <h2>Comptabilité</h2>
      <p style={{ color: '#64748b' }}>
        Module en event sourcing : chaque écriture est un événement immuable ; balance et grands
        livres sont reconstruits en rejouant le journal des événements.
      </p>
      {error && <div className="error">{error}</div>}

      <div className="grid grid-2">
        <div className="card">
          <h3 style={{ marginTop: 0 }}>Balance des comptes</h3>
          <table>
            <thead>
              <tr>
                <th>Compte</th>
                <th>Intitulé</th>
                <th>Type</th>
                <th className="num">Débit</th>
                <th className="num">Crédit</th>
                <th className="num">Solde</th>
              </tr>
            </thead>
            <tbody>
              {(balance.data ?? []).map((account) => (
                <tr key={account.number}>
                  <td>
                    <a
                      href="#"
                      onClick={(e) => {
                        e.preventDefault()
                        showLedger(account.number)
                      }}
                    >
                      {account.number}
                    </a>
                  </td>
                  <td>{account.name}</td>
                  <td>{typeLabels[account.type] ?? account.type}</td>
                  <td className="num">{money(account.debit)}</td>
                  <td className="num">{money(account.credit)}</td>
                  <td className="num">{money(account.balance)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div>
          <div className="card">
            <h3 style={{ marginTop: 0 }}>Écriture manuelle</h3>
            <form onSubmit={postEntry}>
              <label style={{ display: 'block', marginBottom: '0.6rem' }}>
                Libellé
                <input required value={label} onChange={(e) => setLabel(e.target.value)} />
              </label>
              {entryLines.map((line, index) => (
                <div key={index} style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.4rem' }}>
                  <select
                    value={line.account}
                    onChange={(e) => updateLine(index, { account: e.target.value })}
                  >
                    <option value="">Compte…</option>
                    {(balance.data ?? []).map((a) => (
                      <option key={a.number} value={a.number}>
                        {a.number} — {a.name}
                      </option>
                    ))}
                  </select>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    placeholder="Débit"
                    style={{ width: '7rem' }}
                    value={line.debit}
                    onChange={(e) => updateLine(index, { debit: e.target.value, credit: '' })}
                  />
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    placeholder="Crédit"
                    style={{ width: '7rem' }}
                    value={line.credit}
                    onChange={(e) => updateLine(index, { credit: e.target.value, debit: '' })}
                  />
                </div>
              ))}
              <button
                type="button"
                className="secondary small"
                onClick={() => setEntryLines((ls) => [...ls, { account: '', debit: '', credit: '' }])}
              >
                + Ligne
              </button>
              <div style={{ marginTop: '0.8rem' }}>
                <button type="submit">Passer l'écriture</button>
              </div>
            </form>
          </div>

          {ledger && (
            <div className="card">
              <h3 style={{ marginTop: 0 }}>
                Grand livre — {ledger.account} {accountNames.get(ledger.account)}
              </h3>
              <table>
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Libellé</th>
                    <th className="num">Débit</th>
                    <th className="num">Crédit</th>
                    <th className="num">Solde</th>
                  </tr>
                </thead>
                <tbody>
                  {ledger.lines.map((line, i) => (
                    <tr key={i}>
                      <td>{date(line.date)}</td>
                      <td>{line.label}</td>
                      <td className="num">{line.debit ? money(line.debit) : ''}</td>
                      <td className="num">{line.credit ? money(line.credit) : ''}</td>
                      <td className="num">{money(line.runningBalance)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>Journal</h3>
        {(journal.data ?? [])
          .slice()
          .reverse()
          .map((entry) => (
            <div key={entry.entryId} style={{ marginBottom: '0.8rem' }}>
              <strong>
                {date(entry.date)} — {entry.label}
              </strong>
              {entry.reference && <span className="badge blue" style={{ marginLeft: '0.5rem' }}>{entry.reference}</span>}
              <table>
                <tbody>
                  {entry.lines.map((line, i) => (
                    <tr key={i}>
                      <td style={{ paddingLeft: line.credit > 0 ? '2.5rem' : undefined }}>
                        {line.account} — {accountNames.get(line.account) ?? ''}
                      </td>
                      <td className="num">{line.debit ? money(line.debit) : ''}</td>
                      <td className="num">{line.credit ? money(line.credit) : ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}
        {(journal.data ?? []).length === 0 && <p>Aucune écriture pour le moment.</p>}
      </div>
    </div>
  )
}
