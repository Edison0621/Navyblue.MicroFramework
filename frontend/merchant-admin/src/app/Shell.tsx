import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { clearToken, getShopId } from '../shared/auth'
import { useAuth } from './AuthContext'
import { useI18n } from '../shared/i18n/I18nContext'
import { LanguageSwitcher } from '../shared/ui/LanguageSwitcher'

const menuItems = [
  { path: '/', icon: '📊', labelKey: 'nav.dashboard' },
  { path: '/products', icon: '📦', labelKey: 'nav.products' },
  { path: '/orders', icon: '🛒', labelKey: 'nav.orders' },
  { path: '/marketing', icon: '🎯', labelKey: 'nav.marketing' },
  { path: '/shop-settings', icon: '⚙️', labelKey: 'nav.settings' },
  { path: '/audit', icon: '📝', labelKey: 'nav.audit' },
]

const breadcrumbMap: Record<string, string> = {
  '/': 'nav.dashboard',
  '/products': 'nav.products',
  '/orders': 'nav.orders',
  '/marketing': 'nav.marketing',
  '/shop-settings': 'nav.settings',
  '/audit': 'nav.audit',
}

export function Shell() {
  const navigate = useNavigate()
  const location = useLocation()
  const { user } = useAuth()
  const { t } = useI18n()
  
  const handleLogout = () => {
    clearToken()
    navigate('/login')
  }

  const currentPath = location.pathname
  const breadcrumbKey = breadcrumbMap[currentPath] || 'nav.dashboard'

  return (
    <div className="app-layout">
      {/* 侧边栏 */}
      <aside className="sidebar">
        <div className="sidebar-header">
          <Link to="/" className="sidebar-logo">
            <div className="sidebar-logo-icon">M</div>
            <span className="sidebar-logo-text">{t('login.title')}</span>
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
              <span className="menu-text">{t(item.labelKey)}</span>
            </Link>
          ))}
        </nav>
      </aside>

      {/* 主内容区 */}
      <div className="main-layout">
        {/* 顶部导航 */}
        <header className="top-header">
          <div className="header-left">
            <h1 className="header-title">{t(breadcrumbKey)}</h1>
            <div className="header-breadcrumb">
              <span>{t('nav.home') || '首页'}</span>
              <span className="breadcrumb-separator">/</span>
              <span>{t(breadcrumbKey)}</span>
            </div>
          </div>
          
          <div className="header-right">
            <LanguageSwitcher />
            <div className="header-user">
              <div className="user-avatar">
                {user?.username?.charAt(0).toUpperCase() || 'A'}
              </div>
              <div className="user-info">
                <span className="user-name">{user?.username || 'Admin'}</span>
                <span className="user-role">{t('nav.shop') || '店铺'}: {getShopId()}</span>
              </div>
            </div>
            <button type="button" className="btn btn-ghost" onClick={handleLogout}>
              {t('nav.logout')}
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
