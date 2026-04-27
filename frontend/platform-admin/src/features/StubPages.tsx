import { listAudit } from '../shared/audit'
import { useEffect, useState } from 'react'
import { api } from '../shared/api'
import { recordAudit } from '../shared/audit'

export function OrderGovernancePage() {
  const [userId, setUserId] = useState('11111111-1111-1111-1111-111111111111')
  const [shopId, setShopId] = useState('shop-default')
  const [orders, setOrders] = useState<Array<{ id: string; status: string; finalAmount: number }>>([])
  const [afterSales, setAfterSales] = useState<Array<{ orderId: string; afterSaleId: string; status: string }>>([])
  const [error, setError] = useState('')

  const load = async () => {
    try {
      setError('')
      const oq = new URLSearchParams({ page: '1', pageSize: '20' })
      const aq = new URLSearchParams({ page: '1', pageSize: '20' })
      const o = await api.listOrdersByUser(userId, oq)
      const a = await api.listAfterSalesByShop(shopId, aq)
      setOrders(o.items)
      setAfterSales(a.items)
      recordAudit('order.governance.queried', `user=${userId};shop=${shopId}`)
    } catch (e) {
      setError(e instanceof Error ? e.message : '查询失败')
    }
  }

  return (
    <section className="card">
      <h3>订单与售后仲裁</h3>
      <div className="row">
        <input value={userId} onChange={(e) => setUserId(e.target.value)} placeholder="userId" />
        <input value={shopId} onChange={(e) => setShopId(e.target.value)} placeholder="shopId" />
        <button type="button" onClick={() => void load()}>查询</button>
      </div>
      {error && <p className="error">{error}</p>}
      <p>用户订单：</p>
      {orders.map((x) => <p key={x.id}>{x.id} | {x.status} | {x.finalAmount}</p>)}
      <p>店铺售后：</p>
      {afterSales.map((x) => <p key={`${x.orderId}-${x.afterSaleId}`}>{x.orderId}/{x.afterSaleId} | {x.status}</p>)}
    </section>
  )
}

export function MarketingPage() {
  const [code, setCode] = useState('PLAT10')
  const [name, setName] = useState('Platform 10%')
  const [discountType, setDiscountType] = useState('percentage')
  const [discountValue, setDiscountValue] = useState('10')
  const [promotions, setPromotions] = useState<Array<{ code: string; name: string; discountType: string; discountValue: number; isEnabled: boolean }>>([])
  const [validation, setValidation] = useState<string>('')
  const [error, setError] = useState('')

  const load = async () => {
    try {
      const q = new URLSearchParams({ page: '1', pageSize: '20' })
      const res = await api.listPromotions(q)
      setPromotions(res.items)
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    }
  }
  useEffect(() => {
    void load()
  }, [])

  const create = async () => {
    try {
      setError('')
      await api.createPromotion({
        code,
        name,
        discountType,
        discountValue: Number(discountValue),
        startAt: new Date(Date.now() - 3600_000).toISOString(),
        endAt: new Date(Date.now() + 86400_000 * 30).toISOString(),
        isEnabled: true,
      })
      recordAudit('promotion.created', code)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '创建失败')
    }
  }

  const validate = async () => {
    try {
      const result = await api.validatePromotion(code, 299)
      setValidation(JSON.stringify(result))
      recordAudit('promotion.validated', code)
    } catch (e) {
      setError(e instanceof Error ? e.message : '校验失败')
    }
  }

  return (
    <section className="card">
      <h3>营销活动平台</h3>
      <div className="row">
        <input value={code} onChange={(e) => setCode(e.target.value)} placeholder="code" />
        <input value={name} onChange={(e) => setName(e.target.value)} placeholder="name" />
        <select value={discountType} onChange={(e) => setDiscountType(e.target.value)}>
          <option value="percentage">percentage</option>
          <option value="fixed">fixed</option>
        </select>
        <input value={discountValue} onChange={(e) => setDiscountValue(e.target.value)} placeholder="discountValue" />
        <button type="button" onClick={() => void create()}>创建活动</button>
        <button type="button" onClick={() => void validate()}>校验活动</button>
      </div>
      {error && <p className="error">{error}</p>}
      {validation && <p>{validation}</p>}
      {promotions.map((p) => <p key={p.code}>{p.code} | {p.name} | {p.discountType}:{p.discountValue}</p>)}
    </section>
  )
}

export function FinancePage() {
  const [runs, setRuns] = useState<Array<{ id: string; jobName: string; status: string; message: string }>>([])
  const [error, setError] = useState('')

  const loadRuns = async () => {
    try {
      const q = new URLSearchParams({ page: '1', pageSize: '20' })
      const result = await api.listJobRuns(q)
      setRuns(result.items)
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    }
  }

  useEffect(() => {
    void loadRuns()
  }, [])

  const run = async (name: 'reconcile-refunds' | 'expire-awaiting-payments' | 'reclaim-expired-inventory-reservations' | 'apply-catalog-shelf-schedules') => {
    try {
      await api.runJob(name)
      recordAudit('finance.job.run', name)
      await loadRuns()
    } catch (e) {
      setError(e instanceof Error ? e.message : '执行失败')
    }
  }

  return (
    <section className="card">
      <h3>财务结算与对账</h3>
      <div className="row">
        <button type="button" onClick={() => void run('reconcile-refunds')}>退款对账</button>
        <button type="button" onClick={() => void run('expire-awaiting-payments')}>超时关单</button>
        <button type="button" onClick={() => void run('reclaim-expired-inventory-reservations')}>库存回收</button>
      </div>
      {error && <p className="error">{error}</p>}
      {runs.map((r) => <p key={r.id}>{r.jobName} | {r.status} | {r.message}</p>)}
    </section>
  )
}

export function TicketPage() {
  const [tickets, setTickets] = useState([
    { id: 'tk-001', title: '用户投诉商品与描述不符', status: 'open', priority: 'high' },
    { id: 'tk-002', title: '商家发货延迟申诉', status: 'processing', priority: 'medium' },
  ])
  const update = (id: string, status: string) => {
    setTickets((prev) => prev.map((t) => (t.id === id ? { ...t, status } : t)))
    recordAudit('ticket.status.updated', `${id}:${status}`)
  }
  return (
    <section className="card">
      <h3>客服工单</h3>
      {tickets.map((t) => (
        <div className="row" key={t.id}>
          <span>{t.id}</span><span>{t.title}</span><span>{t.priority}</span><span>{t.status}</span>
          <button type="button" onClick={() => update(t.id, 'processing')}>处理中</button>
          <button type="button" onClick={() => update(t.id, 'resolved')}>已解决</button>
        </div>
      ))}
    </section>
  )
}

export function RiskPage() {
  const [words, setWords] = useState('违禁词1,违禁词2')
  const [ruleEnabled, setRuleEnabled] = useState(true)
  const [alerts, setAlerts] = useState<string[]>([])

  const save = () => {
    localStorage.setItem('platform_risk_words', words)
    localStorage.setItem('platform_risk_rule_enabled', ruleEnabled ? '1' : '0')
    recordAudit('risk.config.updated', `enabled=${ruleEnabled};words=${words}`)
    setAlerts((prev) => [`${new Date().toISOString()} saved risk config`, ...prev].slice(0, 20))
  }

  return (
    <section className="card">
      <h3>风控与安全</h3>
      <label>敏感词库</label>
      <input value={words} onChange={(e) => setWords(e.target.value)} />
      <div className="row">
        <label>启用反刷策略</label>
        <input type="checkbox" checked={ruleEnabled} onChange={(e) => setRuleEnabled(e.target.checked)} />
      </div>
      <button type="button" onClick={save}>保存风控配置</button>
      {alerts.map((a) => <p key={a}>{a}</p>)}
    </section>
  )
}

export function SystemPage() {
  const [feeRate, setFeeRate] = useState('0.03')
  const [paymentTimeout, setPaymentTimeout] = useState('30')
  const [regionVersion, setRegionVersion] = useState('cn-mainland-v1')

  const save = () => {
    localStorage.setItem('platform_system_fee_rate', feeRate)
    localStorage.setItem('platform_system_payment_timeout', paymentTimeout)
    localStorage.setItem('platform_system_region_version', regionVersion)
    recordAudit('system.config.updated', `fee=${feeRate};timeout=${paymentTimeout};region=${regionVersion}`)
  }

  return (
    <section className="card">
      <h3>系统设置</h3>
      <div className="row"><label>手续费率</label><input value={feeRate} onChange={(e) => setFeeRate(e.target.value)} /></div>
      <div className="row"><label>支付超时(分钟)</label><input value={paymentTimeout} onChange={(e) => setPaymentTimeout(e.target.value)} /></div>
      <div className="row"><label>地区数据版本</label><input value={regionVersion} onChange={(e) => setRegionVersion(e.target.value)} /></div>
      <button type="button" onClick={save}>保存系统参数</button>
    </section>
  )
}
export function AuditPage() {
  const rows = listAudit()
  return (
    <section className="card">
      <h3>操作审计（前端埋点）</h3>
      {rows.length === 0 ? <p>暂无记录</p> : null}
      {rows.map((r, i) => (
        <p key={`${r.at}-${i}`}>{r.at} | {r.action} | {r.detail}</p>
      ))}
    </section>
  )
}
