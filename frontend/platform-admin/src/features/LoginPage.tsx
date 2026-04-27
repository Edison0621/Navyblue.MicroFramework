import { useState } from 'react'

export function LoginPage() {
  const [token, setToken] = useState(localStorage.getItem('buyer_web_access_token') ?? '')
  return (
    <section className="card">
      <h3>平台端登录</h3>
      <p>当前版本使用管理员 Bearer Token 进入平台。</p>
      <input value={token} onChange={(e) => setToken(e.target.value)} placeholder="粘贴 admin token" />
      <button
        type="button"
        onClick={() => {
          localStorage.setItem('buyer_web_access_token', token)
          window.location.href = '/'
        }}
      >
        保存并进入
      </button>
    </section>
  )
}
