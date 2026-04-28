import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { clearToken } from '../shared/auth'
import { useI18n } from '../shared/i18n/I18nContext'
import { LanguageSwitcher } from '../shared/ui/LanguageSwitcher'

const menuItems = [
  { path: '/', icon: '📊', labelKey: 'nav.dashboard' },
  { path: '/merchant', icon: '🏪', labelKey: 'nav.merchant' },
  { path: '/catalog', icon: '📂', labelKey: 'nav.catalog' },
  { path: '/product-audit', icon: '✅', labelKey: 'nav.productAudit' },
  { path: '/order-governance', icon: '🛒', labelKey: 'nav.orderGovernance' },
  { path: '/user-governance', icon: '👥', labelKey: 'nav.userGovernance' },
  { path: '/marketing', icon: '🎯', labelKey: 'nav.marketing' },
  { path: '/finance', icon: '💰', labelKey: 'nav.finance' },
  { path: '/tickets', icon: '🎫', labelKey: 'nav.tickets' },
  { path: '/risk', icon: '🛡️', labelKey: 'nav.risk' },
  { path: '/system', icon: '⚙️', labelKey: 'nav.system' },
  { path: '/audit', icon: '📝', labelKey: 'nav.audit' },
]

const pathLabels: Record<string, string> = {
  '/': 'nav.dashboard',
  '/merchant': 'nav.merchant',
  '/catalog': 'nav.catalog',
  '/product-audit': 'nav.productAudit',
  '/order-governance': 'nav.orderGovernance',
  '/user-governance': 'nav.userGovernance',
  '/marketing': 'nav.marketing',
  '/finance': 'nav.finance',
  '/tickets': 'nav.tickets',
  '/risk': 'nav.risk',
  '/system': 'nav.system',
  '/audit': 'nav.audit',
}

export function Shell() {
  const navigate = useNavigate()
  const location = useLocation()
  const currentPath = location.pathname
  const { t } = useI18n()

  const handleLogout = () => {
    clearToken()
    navigate('/login')
  }

  const currentLabelKey = pathLabels[currentPath] || 'nav.dashboard'

  return (
    <div className="app-layout">
      {/* 侧边栏 */}
      <aside className="sidebar">
        <div className="sidebar-header">
          <Link to="/" className="sidebar-logo">
            <div className="sidebar-logo-icon">P</div>
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
          <div className="breadcrumb">
            <span>{t('nav.platform') || '平台管理'}</span>
            <span className="breadcrumb-separator">/</span>
            <span className="breadcrumb-current">{t(currentLabelKey)}</span>
          </div>
          <div className="header-actions">
            <LanguageSwitcher />
            <div className="user-info" onClick={handleLogout} title={t('nav.logout')}>
              <div className="user-avatar">A</div>
              <span className="user-name">{t('nav.admin') || '管理员'}</span>
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
