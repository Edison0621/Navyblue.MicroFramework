import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../shared/api'
import { setShopId, setToken } from '../shared/auth'

export function LoginPage() {
  const navigate = useNavigate()
  const [account, setAccount] = useState('admin')
  const [password, setPassword] = useState('admin123')
  const [shop, setShop] = useState('shop-default')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const submit = async () => {
    try {
      setLoading(true)
      setError('')
      const result = await api.login(account.trim(), password)
      setToken(result.accessToken)
      setShopId(shop)
      navigate('/')
    } catch (e) {
      setError(e instanceof Error ? e.message : '登录失败')
    } finally {
      setLoading(false)
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      void submit()
    }
  }

  return (
    <div className="login-page" onKeyDown={handleKeyDown}>
      <div className="login-container">
        <div className="login-header">
          <div className="login-logo">M</div>
          <h1 className="login-title">商家管理后台</h1>
          <p className="login-subtitle">Merchant Admin Portal</p>
        </div>

        <div className="login-card">
          <form className="login-form" onSubmit={(e) => e.preventDefault()}>
            <div className="form-group">
              <label className="form-label form-label-required">账号</label>
              <input
                className="form-input"
                value={account}
                onChange={(e) => setAccount(e.target.value)}
                placeholder="请输入账号"
              />
            </div>

            <div className="form-group">
              <label className="form-label form-label-required">密码</label>
              <input
                className="form-input"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="请输入密码"
              />
            </div>

            <div className="form-group">
              <label className="form-label">店铺ID</label>
              <input
                className="form-input"
                value={shop}
                onChange={(e) => setShop(e.target.value)}
                placeholder="请输入店铺ID"
              />
            </div>

            {error && (
              <div className="message message-error">
                <span>⚠️</span>
                <span>{error}</span>
              </div>
            )}

            <button
              type="button"
              className="btn btn-primary login-button"
              disabled={loading}
              onClick={() => void submit()}
            >
              {loading ? (
                <>
                  <span className="spinner"></span>
                  <span>登录中...</span>
                </>
              ) : (
                '登录'
              )}
            </button>
          </form>
        </div>

        <div className="login-footer">
          <p>默认演示账号：admin / admin123</p>
          <p>© 2024 Merchant Admin. All rights reserved.</p>
        </div>
      </div>
    </div>
  )
}
