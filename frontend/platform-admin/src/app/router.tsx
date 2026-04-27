import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { Shell } from './Shell'
import { LoginPage } from '../features/LoginPage'
import { DashboardPage } from '../features/DashboardPage'
import { MerchantPage } from '../features/MerchantPage'
import { CatalogPage } from '../features/CatalogPage'
import { ProductAuditPage } from '../features/ProductAuditPage'
import { UserGovernancePage } from '../features/UserGovernancePage'
import { AuditPage, FinancePage, MarketingPage, OrderGovernancePage, RiskPage, SystemPage, TicketPage } from '../features/StubPages'

function Guard() {
  const { loading, user } = useAuth()
  if (loading) return <main><section className="card">加载中...</section></main>
  if (!user) return <Navigate to="/login" replace />
  return <Shell />
}

export function AppRouter() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<Guard />}>
        <Route index element={<DashboardPage />} />
        <Route path="merchant" element={<MerchantPage />} />
        <Route path="catalog" element={<CatalogPage />} />
        <Route path="product-audit" element={<ProductAuditPage />} />
        <Route path="order-governance" element={<OrderGovernancePage />} />
        <Route path="user-governance" element={<UserGovernancePage />} />
        <Route path="marketing" element={<MarketingPage />} />
        <Route path="finance" element={<FinancePage />} />
        <Route path="tickets" element={<TicketPage />} />
        <Route path="risk" element={<RiskPage />} />
        <Route path="system" element={<SystemPage />} />
        <Route path="audit" element={<AuditPage />} />
      </Route>
    </Routes>
  )
}
