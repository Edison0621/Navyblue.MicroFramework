import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { clearToken, getShopId } from '../shared/auth'
import { useAuth } from './AuthContext'

const menuItems = [
  { path: '/', icon: '📊', label: '运营总览' },
  { path: '/products', icon: '📦', label: '商品管理' },
  { path: '/orders', icon: '🛒', label: '订单售后' },
  { path: '/marketing', icon: '🎯', label: '店铺营销' },
  { path: '/shop-settings', icon: '⚙️', label: '店铺设置' },
  { path: '/audit', icon: '📝', label: '操作记录' },
]

const breadcrumbMap: Record<string, string> = {
  '/': '运营总览',
  '/products': '商品管理',
  '/orders': '订单售后',
  '/marketing': '店铺营销',
  '/shop-settings': '店铺设置',
  '/audit': '操作记录',
}

export function Shell() {
  const navigate = useNavigate()
  const location = useLocation()
  const { user } = useAuth()
  
  const handleLogout = () => {
    clearToken()
    navigate('/login')
  }

  const currentPath = location.pathname
  const breadcrumb = breadcrumbMap[currentPath] || '总览'

  return (
    <div className="app-layout">
      {/* 侧边栏 */}
      <aside className="sidebar">
        <div className="sidebar-header">
          <Link to="/" className="sidebar-logo">
            <div className="sidebar-logo-icon">M</div>
            <span className="sidebar-logo-text">商家管理后台</span>
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
          <div className="header-left">
            <h1 className="header-title">{breadcrumb}</h1>
            <div className="header-breadcrumb">
              <span>首页</span>
              <span className="breadcrumb-separator">/</span>
              <span>{breadcrumb}</span>
            </div>
          </div>
          
          <div className="header-right">
            <div className="header-user">
              <div className="user-avatar">
                {user?.username?.charAt(0).toUpperCase() || 'A'}
              </div>
              <div className="user-info">
                <span className="user-name">{user?.username || 'Admin'}</span>
                <span className="user-role">店铺: {getShopId()}</span>
              </div>
            </div>
            <button type="button" className="btn btn-ghost" onClick={handleLogout}>
              退出登录
            </button>
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
