import { useState } from 'react'
import { api } from '../shared/api'
import { recordAudit } from '../shared/audit'
import type { PromotionSummary } from '../types'

export function MarketingPage() {
  const [promotions, setPromotions] = useState<PromotionSummary[]>([])
  const [code, setCode] = useState('PLAT10')
  const [orderAmount, setOrderAmount] = useState('299')
  const [result, setResult] = useState('')
  const [error, setError] = useState('')

  const load = async () => {
    try {
      setError('')
      const q = new URLSearchParams({ page: '1', pageSize: '20' })
      const res = await api.listPromotions(q)
      setPromotions(res.items)
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    }
  }

  const validate = async () => {
    try {
      setError('')
      const v = await api.validatePromotion(code.trim(), Number(orderAmount))
      setResult(JSON.stringify(v))
      recordAudit('merchant.promotion.validated', code)
    } catch (e) {
      setError(e instanceof Error ? e.message : '校验失败')
    }
  }

  return (
    <section className="card">
      <h3>店铺营销</h3>
      <div className="row">
        <button type="button" onClick={() => void load()}>加载可用活动</button>
      </div>
      <div className="row">
        <input value={code} onChange={(e) => setCode(e.target.value)} placeholder="活动码" />
        <input value={orderAmount} onChange={(e) => setOrderAmount(e.target.value)} placeholder="订单金额" />
        <button type="button" onClick={() => void validate()}>校验活动</button>
      </div>
      {error && <p className="error">{error}</p>}
      {result && <p>{result}</p>}
      {promotions.map((p) => (
        <p key={p.code}>{p.code} | {p.name} | {p.discountType}:{p.discountValue}</p>
      ))}
    </section>
  )
}
