import { useState } from 'react'
import { api } from '../shared/api'
import { setToken } from '../shared/auth'

export function LoginPage() {
  const [account, setAccount] = useState('admin')
  const [password, setPassword] = useState('admin123')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const submit = async () => {
    setLoading(true)
    setError('')
    try {
      const result = await api.login(account.trim(), password)
      setToken(result.accessToken)
      window.location.href = '/'
    } catch (e) {
      setError(e instanceof Error ? e.message : '登录失败')
    } finally {
      setLoading(false)
    }
  }

  return (
    <section className="card">
      <h3>平台端登录</h3>
      <p>请输入管理员账号密码登录平台。</p>
      <div className="row">
        <input value={account} onChange={(e) => setAccount(e.target.value)} placeholder="账号" />
        <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="密码" />
        <button type="button" onClick={() => void submit()} disabled={loading}>
          {loading ? '登录中...' : '登录并进入'}
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
    </section>
  )
}
