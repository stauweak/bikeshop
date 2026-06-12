import { NavLink, Navigate, Route, Routes } from 'react-router-dom'
import AccountingPage from './pages/AccountingPage'
import DashboardPage from './pages/DashboardPage'
import InventoryPage from './pages/InventoryPage'
import InvoicingPage from './pages/InvoicingPage'
import PosPage from './pages/PosPage'
import ProcurementPage from './pages/ProcurementPage'
import WorkshopPage from './pages/WorkshopPage'

export default function App() {
  return (
    <div className="layout">
      <aside className="sidebar">
        <h1>🚲 Bikeshop</h1>
        <nav>
          <NavLink to="/" end>Tableau de bord</NavLink>
          <NavLink to="/caisse">Caisse</NavLink>
          <NavLink to="/stocks">Stocks</NavLink>
          <NavLink to="/atelier">Atelier</NavLink>
          <NavLink to="/factures">Facturation</NavLink>
          <NavLink to="/approvisionnement">Approvisionnement</NavLink>
          <NavLink to="/comptabilite">Comptabilité</NavLink>
        </nav>
      </aside>
      <main className="content">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/caisse" element={<PosPage />} />
          <Route path="/stocks" element={<InventoryPage />} />
          <Route path="/atelier" element={<WorkshopPage />} />
          <Route path="/factures" element={<InvoicingPage />} />
          <Route path="/approvisionnement" element={<ProcurementPage />} />
          <Route path="/comptabilite" element={<AccountingPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  )
}
