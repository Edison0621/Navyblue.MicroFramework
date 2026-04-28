import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { useI18n } from '../shared/i18n/I18nContext'
import { LanguageSwitcher } from '../shared/ui/LanguageSwitcher'

export function AppLayout() {
  const { signOut, isAuthed } = useAuth()
  const { t } = useI18n()
  const navigate = useNavigate()
  const location = useLocation()

  return (
    <div className="app-shell">
      <div className="top-strip">
        <div className="top-strip-inner">
          <span>{t('home.welcome') || '欢迎来到 Buyer Mall，品质好货天天低价'}</span>
          <div className="row" style={{ gap: '12px' }}>
            <LanguageSwitcher />
            {isAuthed ? <span>{t('nav.loggedin') || '已登录'}</span> : <span>{t('nav.guest') || '游客访问中'}</span>}
            {isAuthed ? (
              <button
                type="button"
                onClick={() => {
                  signOut()
                  navigate('/')
                }}
              >
                {t('nav.logout')}
              </button>
            ) : (
              <Link className="btn" to="/login">
                {t('nav.login')}
              </Link>
            )}
          </div>
        </div>
      </div>

      <div className="main-header-wrap">
        <header>
          <div className="main-header">
            <Link to="/" className="brand">
              BUYER MALL
            </Link>
            <form
              className="search-panel"
              onSubmit={(event) => {
                event.preventDefault()
                const data = new FormData(event.currentTarget)
                const q = String(data.get('q') ?? '').trim()
                navigate(q ? `/products?q=${encodeURIComponent(q)}` : '/products')
              }}
            >
              <input name="q" placeholder="搜索商品、品牌、店铺" />
              <button type="submit" className="btn-primary">
                搜索
              </button>
            </form>
            <div className="row">
              <Link className="btn-ghost btn" to="/cart">
                购物车
              </Link>
              {isAuthed ? (
                <Link className="btn" to="/orders">
                  我的订单
                </Link>
              ) : (
                <Link className="btn" to={`/login?from=${encodeURIComponent(location.pathname)}`}>
                  登录后下单
                </Link>
              )}
            </div>
          </div>
          <nav className="main-nav">
            <NavLink to="/">首页</NavLink>
            <NavLink to="/products">商品</NavLink>
            <NavLink to="/cart">购物车</NavLink>
            <NavLink to="/orders">订单</NavLink>
            <NavLink to="/profile">个人中心</NavLink>
            <NavLink to="/membership">会员</NavLink>
            <NavLink to="/assets">资产</NavLink>
            <NavLink to="/activity">足迹收藏</NavLink>
            <NavLink to="/security">账户安全</NavLink>
          </nav>
        </header>
      </div>
      <main>
        <Outlet />
      </main>
      <footer>Copyright © Buyer Mall. JD-style redesign preview build.</footer>
    </div>
  )
}
