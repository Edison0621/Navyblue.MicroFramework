import { Link, Outlet, useNavigate } from 'react-router-dom'
import { clearToken, getShopId } from '../shared/auth'

export function Shell() {
  const navigate = useNavigate()
  return (
    <>
      <header>
        <h2>商家管理后台</h2>
        <div className="row">
          <Link to="/">总览</Link>
          <Link to="/products">商品管理</Link>
          <Link to="/orders">订单售后</Link>
          <Link to="/marketing">店铺营销</Link>
          <Link to="/shop-settings">店铺设置</Link>
          <Link to="/audit">操作记录</Link>
          <span className="muted">shopId: {getShopId()}</span>
          <button
            type="button"
            onClick={() => {
              clearToken()
              navigate('/login')
            }}
          >
            退出
          </button>
        </div>
      </header>
      <main>
        <Outlet />
      </main>
    </>
  )
}
