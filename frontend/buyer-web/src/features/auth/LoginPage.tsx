import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { api } from '../../lib/api'
import { useAuth } from '../../app/AuthContext'
import { useToast } from '../../shared/ui/ToastProvider'

export function LoginPage() {
  const [account, setAccount] = useState('demo')
  const [password, setPassword] = useState('demo123')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [loginMethod, setLoginMethod] = useState<'account' | 'sms'>('account')
  const { signIn } = useAuth()
  const { notify } = useToast()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError('')
    try {
      const result = await api.login(account, password)
      signIn(result.accessToken)
      notify('登录成功，欢迎回来！', 'success')
      navigate(searchParams.get('from') || '/')
    } catch (err) {
      const message = err instanceof Error ? err.message : '登录失败，请检查账号密码'
      setError(message)
      notify(message, 'error')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page-wrapper">
      {/* 顶部导航 */}
      <div className="login-header-bar">
        <div className="login-header-content">
          <Link to="/" className="login-brand">
            <span className="brand-logo">M</span>
            <span className="brand-text">BUYER MALL</span>
          </Link>
        </div>
      </div>

      {/* 登录主体 */}
      <div className="login-main-container">
        <div className="login-content-wrapper">
          {/* 左侧品牌区 */}
          <div className="login-brand-section">
            <div className="brand-animation">
              <h1 className="brand-title">品质生活，从这里开始</h1>
              <p className="brand-subtitle">Quality Life Starts Here</p>
              <div className="brand-features">
                <div className="feature-item">
                  <span className="feature-icon">🚀</span>
                  <span>极速配送</span>
                </div>
                <div className="feature-item">
                  <span className="feature-icon">💎</span>
                  <span>品质保证</span>
                </div>
                <div className="feature-item">
                  <span className="feature-icon">🎁</span>
                  <span>专属优惠</span>
                </div>
              </div>
            </div>
          </div>

          {/* 右侧登录表单 */}
          <div className="login-form-section">
            <div className="login-card">
              <div className="login-card-header">
                <h2 className="login-title">欢迎登录</h2>
                <div className="login-method-tabs">
                  <button
                    type="button"
                    className={`tab-btn ${loginMethod === 'account' ? 'active' : ''}`}
                    onClick={() => setLoginMethod('account')}
                  >
                    账号密码登录
                  </button>
                  <button
                    type="button"
                    className={`tab-btn ${loginMethod === 'sms' ? 'active' : ''}`}
                    onClick={() => setLoginMethod('sms')}
                  >
                    短信验证码登录
                  </button>
                </div>
              </div>

              {loginMethod === 'account' ? (
                <form className="login-form" onSubmit={submit}>
                  <div className="form-group">
                    <label className="form-label">账号</label>
                    <input
                      className="form-input"
                      value={account}
                      onChange={(e) => setAccount(e.target.value)}
                      placeholder="请输入手机号/邮箱/用户名"
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

                  <div className="form-actions">
                    <button
                      type="submit"
                      className="btn btn-primary btn-large login-submit-btn"
                      disabled={loading}
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
                  </div>

                  <div className="login-links">
                    <a href="#" className="link">忘记密码</a>
                    <a href="#" className="link">免费注册</a>
                  </div>
                </form>
              ) : (
                <form className="login-form" onSubmit={(e) => e.preventDefault()}>
                  <div className="form-group">
                    <label className="form-label">手机号</label>
                    <input
                      className="form-input"
                      type="tel"
                      placeholder="请输入手机号"
                    />
                  </div>

                  <div className="form-group">
                    <label className="form-label">验证码</label>
                    <div className="sms-input-group">
                      <input
                        className="form-input"
                        placeholder="请输入验证码"
                      />
                      <button type="button" className="btn btn-ghost sms-code-btn">
                        获取验证码
                      </button>
                    </div>
                  </div>

                  <div className="form-actions">
                    <button
                      type="submit"
                      className="btn btn-primary btn-large login-submit-btn"
                    >
                      登录
                    </button>
                  </div>

                  <div className="login-links">
                    <a href="#" className="link">忘记密码</a>
                    <a href="#" className="link">免费注册</a>
                  </div>
                </form>
              )}

              {/* 第三方登录 */}
              <div className="third-party-login">
                <div className="divider">
                  <span className="divider-text">其他登录方式</span>
                </div>
                <div className="third-party-icons">
                  <button type="button" className="third-party-btn" title="微信登录">
                    <span className="icon">💬</span>
                  </button>
                  <button type="button" className="third-party-btn" title="QQ登录">
                    <span className="icon">🐧</span>
                  </button>
                  <button type="button" className="third-party-btn" title="微博登录">
                    <span className="icon">📱</span>
                  </button>
                </div>
              </div>
            </div>

            {/* 登录提示 */}
            <div className="login-tips">
              <p className="muted">
                登录即表示您同意我们的
                <a href="#" className="link">用户协议</a>
                和
                <a href="#" className="link">隐私政策</a>
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* 底部 */}
      <footer className="login-footer">
        <p>© 2024 Buyer Mall. All rights reserved.</p>
      </footer>
    </div>
  )
}
