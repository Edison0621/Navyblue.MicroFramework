import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api } from '../../lib/api'
import { useAuth } from '../../app/AuthContext'
import { useToast } from '../../shared/ui/ToastProvider'
import { PageHeader, SurfaceCard } from '../../shared/ui/Storefront'

export function LoginPage() {
  const [account, setAccount] = useState('demo')
  const [password, setPassword] = useState('demo123')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
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
      notify('登录成功', 'success')
      navigate(searchParams.get('from') || '/')
    } catch (err) {
      const message = err instanceof Error ? err.message : '登录失败'
      setError(message)
      notify(message, 'error')
    } finally {
      setLoading(false)
    }
  }

  return (
    <section>
      <PageHeader title="登录" subtitle="登录后可下单、查看订单与个人资产" />
      <SurfaceCard>
        <form onSubmit={submit}>
          <label>
            账号
            <input value={account} onChange={(e) => setAccount(e.target.value)} />
          </label>
          <label>
            密码
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </label>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? '登录中...' : '登录'}
          </button>
        </form>
        <p className="muted">短信登录/第三方登录/找回密码：当前版本为占位入口，后续接入。</p>
        {error && <p className="error">{error}</p>}
      </SurfaceCard>
    </section>
  )
}
