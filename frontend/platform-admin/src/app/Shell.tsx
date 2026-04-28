import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { clearToken } from '../shared/auth'

const menuItems = [
  { path: '/', icon: '📊', label: '运营总览' },
  { path: '/merchant', icon: '🏪', label: '商家管理' },
  { path: '/catalog', icon: '📂', label: '类目属性' },
  { path: '/product-audit', icon: '✅', label: '商品审核' },
  { path: '/order-governance', icon: '🛒', label: '订单仲裁' },
  { path: '/user-governance', icon: '👥', label: '用户治理' },
  { path: '/marketing', icon: '🎯', label: '营销活动' },
  { path: '/finance', icon: '💰', label: '财务结算' },
  { path: '/tickets', icon: '🎫', label: '客服工单' },
  { path: '/risk', icon: '🛡️', label: '风控安全' },
  { path: '/system', icon: '⚙️', label: '系统设置' },
  { path: '/audit', icon: '📝', label: '操作审计' },
]

const pathLabels: Record<string, string> = {
  '/': '运营总览',
  '/merchant': '商家管理',
  '/catalog': '类目属性',
  '/product-audit': '商品审核',
  '/order-governance': '订单仲裁',
  '/user-governance': '用户治理',
  '/marketing': '营销活动',
  '/finance': '财务结算',
  '/tickets': '客服工单',
  '/risk': '风控安全',
  '/system': '系统设置',
  '/audit': '操作审计',
}

export function Shell() {
  const navigate = useNavigate()
  const location = useLocation()
  const currentPath = location.pathname

  const handleLogout = () => {
    clearToken()
    navigate('/login')
  }

  const currentLabel = pathLabels[currentPath] || '平台管理'
  const menuItem = menuItems.find(item => item.path === currentPath)

  return (
    <div className="app-layout">
      {/* 侧边栏 */}
      <aside className="sidebar">
        <div className="sidebar-header">
          <Link to="/" className="sidebar-logo">
            <div className="sidebar-logo-icon">P</div>
            <span className="sidebar-logo-text">平台管理后台</span>
          </Link>
        </div>
        <nav className="sidebar-menu">
          {menuItems.map((item) => (
            <Link
              key={item.path}
              to={item.path}
              className={`menu-item ${currentPath === item.path ? 'active' : ''}`}
            >
              <span className="menu-icon">{item.icon}</span>
              <span className="menu-text">{item.label}</span>
            </Link>
          ))}
        </nav>
      </aside>

      {/* 主内容区 */}
      <div className="main-layout">
        {/* 顶部导航 */}
        <header className="top-header">
          <div className="breadcrumb">
            <span>平台管理</span>
            <span className="breadcrumb-separator">/</span>
            <span className="breadcrumb-current">{currentLabel}</span>
          </div>
          <div className="header-actions">
            <div className="user-info" onClick={handleLogout} title="点击退出登录">
              <div className="user-avatar">A</div>
              <span className="user-name">管理员</span>
            </div>
          </div>
        </header>

        {/* 页面内容 */}
        <main className="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
