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
  const [message, setMessage] = useState('')

  const load = async () => {
    try {
      setError('')
      setMessage('')
      const q = new URLSearchParams({ page: '1', pageSize: '20' })
      const res = await api.listPromotions(q)
      setPromotions(res.items)
      setMessage('加载成功')
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    }
  }

  const validate = async () => {
    try {
      setError('')
      setMessage('')
      const v = await api.validatePromotion(code.trim(), Number(orderAmount))
      setResult(JSON.stringify(v, null, 2))
      recordAudit('merchant.promotion.validated', code)
      setMessage('校验成功')
    } catch (e) {
      setError(e instanceof Error ? e.message : '校验失败')
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">店铺营销</h1>
        <p className="page-description">管理促销活动和优惠码</p>
      </div>

      {error && (
        <div className="message message-error">
          <span>⚠️</span>
          <span>{error}</span>
        </div>
      )}

      {message && (
        <div className="message message-success">
          <span>✅</span>
          <span>{message}</span>
        </div>
      )}

      {/* 活动校验 */}
      <div className="card">
        <div className="card-header">
          <h3 className="card-title">活动校验</h3>
        </div>
        <div className="card-body">
          <div className="form-row">
            <div className="form-group">
              <label className="form-label form-label-required">活动码</label>
              <input
                className="form-input"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                placeholder="请输入活动码"
              />
            </div>

            <div className="form-group">
              <label className="form-label form-label-required">订单金额</label>
              <input
                className="form-input"
                value={orderAmount}
                onChange={(e) => setOrderAmount(e.target.value)}
                placeholder="请输入订单金额"
                type="number"
              />
            </div>
          </div>

          <div className="toolbar" style={{ marginTop: '16px' }}>
            <button className="btn btn-primary" onClick={() => void validate()}>
              🔍 校验活动
            </button>
          </div>

          {result && (
            <div style={{ marginTop: '24px' }}>
              <label className="form-label">校验结果</label>
              <pre style={{
                background: 'var(--bg-hover)',
                padding: '16px',
                borderRadius: 'var(--radius-md)',
                overflow: 'auto',
                fontSize: 'var(--font-size-sm)',
              }}>
                {result}
              </pre>
            </div>
          )}
        </div>
      </div>

      {/* 活动列表 */}
      <div className="card">
        <div className="card-header">
          <h3 className="card-title">可用活动</h3>
          <div className="card-extra">
            <button className="btn btn-sm" onClick={() => void load()}>
              🔄 刷新
            </button>
          </div>
        </div>
        <div className="card-body">
          {promotions.length === 0 ? (
            <div className="empty-state">
              <div className="empty-icon">🎯</div>
              <p className="empty-text">暂无活动</p>
              <button className="btn btn-primary" onClick={() => void load()}>
                加载活动
              </button>
            </div>
          ) : (
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th>活动码</th>
                    <th>活动名称</th>
                    <th>折扣类型</th>
                    <th>折扣值</th>
                  </tr>
                </thead>
                <tbody>
                  {promotions.map((p) => (
                    <tr key={p.code}>
                      <td>
                        <span className="badge badge-primary">{p.code}</span>
                      </td>
                      <td>{p.name}</td>
                      <td>{p.discountType}</td>
                      <td>
                        <span className="text-primary" style={{ fontWeight: 600 }}>
                          {p.discountValue}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
