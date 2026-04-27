import { Link, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from './AuthContext'

export function AppLayout() {
  const { signOut } = useAuth()
  const navigate = useNavigate()

  return (
    <div className="app-shell">
      <header>
        <h1>Buyer Web</h1>
        <nav>
          <Link to="/products">商品</Link>
          <Link to="/cart">购物车</Link>
          <Link to="/orders">订单</Link>
          <Link to="/profile">我的</Link>
          <Link to="/membership">会员</Link>
          <Link to="/assets">资产</Link>
          <Link to="/activity">足迹收藏</Link>
          <button
            type="button"
            onClick={() => {
              signOut()
              navigate('/login')
            }}
          >
            退出
          </button>
        </nav>
      </header>
      <main>
        <Outlet />
      </main>
    </div>
  )
}
