import { useState, type FormEvent } from 'react'
import { api } from '../shared/api'
import { setToken } from '../shared/auth'

export function LoginPage() {
  const [account, setAccount] = useState('admin')
  const [password, setPassword] = useState('admin123')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError('')
    try {
      const result = await api.login(account.trim(), password)
      setToken(result.accessToken)
      window.location.href = '/'
    } catch (e) {
      setError(e instanceof Error ? e.message : '登录失败，请检查账号密码')
    } finally {
      setLoading(false)
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSubmit(e as unknown as FormEvent)
    }
  }

  return (
    <div className="login-page" onKeyDown={handleKeyDown}>
      <div className="login-container">
        <div className="login-header">
          <div className="login-logo">P</div>
          <h1 className="login-title">平台管理后台</h1>
          <p className="login-subtitle">PLATFORM ADMIN PORTAL</p>
        </div>

        <div className="login-card">
          <form className="login-form" onSubmit={handleSubmit}>
            <div className="form-group">
              <label className="form-label">管理员账号</label>
              <input
                className="form-input"
                value={account}
                onChange={(e) => setAccount(e.target.value)}
                placeholder="请输入管理员账号"
              />
            </div>

            <div className="form-group">
              <label className="form-label">密码</label>
              <input
                className="form-input"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="请输入密码"
              />
            </div>

            {error && (
              <div className="message message-error">
                <span>⚠️</span>
                <span>{error}</span>
              </div>
            )}

            <button
              type="submit"
              className="btn btn-primary login-submit-btn"
              disabled={loading}
            >
              {loading ? (
                <>
                  <span className="spinner"></span>
                  <span>登录中...</span>
                </>
              ) : (
                '登录并进入'
              )}
            </button>
          </form>

          <div className="login-tips">
            <p>默认账号：admin / admin123</p>
          </div>
        </div>
      </div>
    </div>
  )
}
