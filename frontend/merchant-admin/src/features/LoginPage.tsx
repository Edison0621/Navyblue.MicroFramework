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

  return (
    <main>
      <section className="card">
        <h3>商家登录</h3>
        <p className="muted">默认演示账号可使用 admin/admin123，店铺编号默认 shop-default。</p>
        <div className="row">
          <input value={account} onChange={(e) => setAccount(e.target.value)} placeholder="账号" />
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="密码" />
          <input value={shop} onChange={(e) => setShop(e.target.value)} placeholder="shopId" />
          <button type="button" disabled={loading} onClick={() => void submit()}>{loading ? '登录中...' : '登录并进入'}</button>
        </div>
        {error && <p className="error">{error}</p>}
      </section>
    </main>
  )
}
