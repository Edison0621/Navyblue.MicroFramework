import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { AppLayout } from './AppLayout'
import { LoginPage } from '../features/auth/LoginPage'
import { HomePage, ProductDetailPage, ProductListPage } from '../features/catalog/CatalogPages'
import { CartPage, CheckoutPage } from '../features/trade/TradePages'
import { OrderDetailPage, OrdersPage } from '../features/orders/OrderPages'
import { InvoicePage, ProfilePage } from '../features/profile/ProfilePages'
import { SecurityPage } from '../features/profile/SecurityPage'
import { MembershipPage } from '../features/membership/MembershipPages'
import { AssetsPage } from '../features/assets/AssetsPage'
import { ActivityPage } from '../features/activity/ActivityPage'

function RequireAuth() {
  const { isAuthed } = useAuth()
  return isAuthed ? <Outlet /> : <Navigate to="/login" replace />
}

function ComingSoon() {
  return (
    <section className="card">
      <h2>功能规划中</h2>
      <p className="muted">该功能已预留路由与页面入口，后续迭代完善。</p>
    </section>
  )
}

export function AppRouter() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<AppLayout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/products" element={<ProductListPage />} />
        <Route path="/products/:id" element={<ProductDetailPage />} />
        <Route path="/cart" element={<CartPage />} />
        <Route path="/checkout" element={<CheckoutPage />} />
        <Route element={<RequireAuth />}>
          <Route path="/orders" element={<OrdersPage />} />
          <Route path="/orders/:id" element={<OrderDetailPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/profile/invoices" element={<InvoicePage />} />
          <Route path="/membership" element={<MembershipPage />} />
          <Route path="/assets" element={<AssetsPage />} />
          <Route path="/activity" element={<ActivityPage />} />
          <Route path="/security" element={<SecurityPage />} />
          <Route path="/after-sale" element={<ComingSoon />} />
          <Route path="/reviews" element={<ComingSoon />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
