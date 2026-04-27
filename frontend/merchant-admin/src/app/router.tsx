import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { Shell } from './Shell'
import { LoginPage } from '../features/LoginPage'
import { DashboardPage } from '../features/DashboardPage'
import { ProductPage } from '../features/ProductPage'
import { OrdersPage } from '../features/OrdersPage'
import { MarketingPage } from '../features/MarketingPage'
import { ShopSettingsPage } from '../features/ShopSettingsPage'
import { AuditPage } from '../features/AuditPage'

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
        <Route path="products" element={<ProductPage />} />
        <Route path="orders" element={<OrdersPage />} />
        <Route path="marketing" element={<MarketingPage />} />
        <Route path="shop-settings" element={<ShopSettingsPage />} />
        <Route path="audit" element={<AuditPage />} />
      </Route>
    </Routes>
  )
}
