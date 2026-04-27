import { useState } from 'react'
import { recordAudit } from '../shared/audit'

type Merchant = { id: string; name: string; status: 'normal' | 'frozen' | 'closed'; violationScore: number }

export function MerchantPage() {
  const [items, setItems] = useState<Merchant[]>([
    { id: 'm-100', name: '商家A', status: 'normal', violationScore: 0 },
    { id: 'm-200', name: '商家B', status: 'frozen', violationScore: 8 },
  ])
  const setStatus = (id: string, status: Merchant['status']) => {
    setItems((prev) => prev.map((x) => (x.id === id ? { ...x, status } : x)))
    recordAudit('merchant.status.updated', `${id} -> ${status}`)
  }
  return (
    <section className="card">
      <h3>商家管理</h3>
      {items.map((m) => (
        <div className="row" key={m.id}>
          <span>{m.name}</span>
          <span>{m.status}</span>
          <span>违规分 {m.violationScore}</span>
          <button type="button" onClick={() => setStatus(m.id, 'normal')}>正常</button>
          <button type="button" onClick={() => setStatus(m.id, 'frozen')}>冻结</button>
          <button type="button" onClick={() => setStatus(m.id, 'closed')}>关闭</button>
        </div>
      ))}
    </section>
  )
}
