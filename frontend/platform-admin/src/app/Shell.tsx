import { Link, Outlet, useNavigate } from 'react-router-dom'
import { clearToken } from '../shared/auth'

export function Shell() {
  const navigate = useNavigate()
  return (
    <>
      <header>
        <h2>平台端管理后台</h2>
        <div className="row">
          <Link to="/">总览</Link>
          <Link to="/merchant">商家管理</Link>
          <Link to="/catalog">类目属性</Link>
          <Link to="/product-audit">商品审核</Link>
          <Link to="/order-governance">订单仲裁</Link>
          <Link to="/user-governance">用户管理</Link>
          <Link to="/marketing">营销</Link>
          <Link to="/finance">财务</Link>
          <Link to="/tickets">客服工单</Link>
          <Link to="/risk">风控安全</Link>
          <Link to="/system">系统设置</Link>
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
