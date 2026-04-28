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
  const [loading, setLoading] = useState(false)
  const [activeTab, setActiveTab] = useState<'orders' | 'aftersales'>('orders')

  const load = async () => {
    try {
      setLoading(true)
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
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="card">
      <h3 className="card-title">订单与售后仲裁</h3>
      <p className="card-subtitle">处理平台订单争议和售后申请</p>

      {/* 查询表单 */}
      <div className="toolbar">
        <div className="toolbar-left">
          <div className="form-group" style={{ marginBottom: 0, minWidth: '300px' }}>
            <label className="form-label">用户ID</label>
            <input
              className="form-input"
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
              placeholder="请输入用户ID"
            />
          </div>
          <div className="form-group" style={{ marginBottom: 0, minWidth: '200px' }}>
            <label className="form-label">店铺ID</label>
            <input
              className="form-input"
              value={shopId}
              onChange={(e) => setShopId(e.target.value)}
              placeholder="请输入店铺ID"
            />
          </div>
        </div>
        <div className="toolbar-right">
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => void load()}
            disabled={loading}
          >
            {loading ? (
              <>
                <span className="spinner"></span>
                <span>查询中...</span>
              </>
            ) : (
              '查询'
            )}
          </button>
        </div>
      </div>

      {error && <div className="message message-error">⚠️ {error}</div>}

      {/* Tabs 切换 */}
      <div className="tabs">
        <div className="tab-list">
          <button
            type="button"
            className={`tab-btn ${activeTab === 'orders' ? 'active' : ''}`}
            onClick={() => setActiveTab('orders')}
          >
            用户订单 ({orders.length})
          </button>
          <button
            type="button"
            className={`tab-btn ${activeTab === 'aftersales' ? 'active' : ''}`}
            onClick={() => setActiveTab('aftersales')}
          >
            店铺售后 ({afterSales.length})
          </button>
        </div>
      </div>

      {/* 订单列表 */}
      {activeTab === 'orders' && (
        <>
          {orders.length > 0 ? (
            <div className="table-wrapper">
              <table>
                <thead>
                  <tr>
                    <th>订单ID</th>
                    <th>订单状态</th>
                    <th>订单金额</th>
                  </tr>
                </thead>
                <tbody>
                  {orders.map((x) => (
                    <tr key={x.id}>
                      <td className="text-tertiary">{x.id}</td>
                      <td>
                        <span className="badge badge-primary">{x.status}</span>
                      </td>
                      <td><strong>￥{x.finalAmount.toFixed(2)}</strong></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="empty-state">
              <div className="empty-state-icon">🛒</div>
              <div className="empty-state-text">暂无订单数据</div>
            </div>
          )}
        </>
      )}

      {/* 售后列表 */}
      {activeTab === 'aftersales' && (
        <>
          {afterSales.length > 0 ? (
            <div className="table-wrapper">
              <table>
                <thead>
                  <tr>
                    <th>订单ID</th>
                    <th>售后ID</th>
                    <th>售后状态</th>
                  </tr>
                </thead>
                <tbody>
                  {afterSales.map((x) => (
                    <tr key={`${x.orderId}-${x.afterSaleId}`}>
                      <td className="text-tertiary">{x.orderId}</td>
                      <td className="text-tertiary">{x.afterSaleId}</td>
                      <td>
                        <span className="badge badge-warning">{x.status}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="empty-state">
              <div className="empty-state-icon">📋</div>
              <div className="empty-state-text">暂无售后数据</div>
            </div>
          )}
        </>
      )}
    </div>
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
  const [loading, setLoading] = useState(false)

  const load = async () => {
    try {
      setLoading(true)
      const q = new URLSearchParams({ page: '1', pageSize: '20' })
      const res = await api.listPromotions(q)
      setPromotions(res.items)
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    } finally {
      setLoading(false)
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
      setValidation(JSON.stringify(result, null, 2))
      recordAudit('promotion.validated', code)
    } catch (e) {
      setError(e instanceof Error ? e.message : '校验失败')
    }
  }

  return (
    <div className="card">
      <h3 className="card-title">营销活动平台</h3>
      <p className="card-subtitle">创建和管理平台级促销活动</p>

      {error && <div className="message message-error">⚠️ {error}</div>}

      {/* 创建表单 */}
      <div className="form-group">
        <label className="form-label">活动编码</label>
        <input
          className="form-input"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          placeholder="例如：PLAT10"
        />
      </div>

      <div className="form-group">
        <label className="form-label">活动名称</label>
        <input
          className="form-input"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="例如：Platform 10%"
        />
      </div>

      <div className="grid grid-2">
        <div className="form-group">
          <label className="form-label">折扣类型</label>
          <select
            className="form-select"
            value={discountType}
            onChange={(e) => setDiscountType(e.target.value)}
          >
            <option value="percentage">百分比折扣</option>
            <option value="fixed">固定金额</option>
          </select>
        </div>

        <div className="form-group">
          <label className="form-label">折扣值</label>
          <input
            className="form-input"
            value={discountValue}
            onChange={(e) => setDiscountValue(e.target.value)}
            placeholder="10"
          />
        </div>
      </div>

      <div className="action-group">
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => void create()}
        >
          创建活动
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          onClick={() => void validate()}
        >
          校验活动
        </button>
      </div>

      {/* 校验结果 */}
      {validation && (
        <div className="message message-success">
          <div>
            <strong>校验结果：</strong>
            <pre style={{ marginTop: '8px', whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
              {validation}
            </pre>
          </div>
        </div>
      )}

      {/* 活动列表 */}
      {promotions.length > 0 && (
        <>
          <h4 style={{ marginTop: '24px', marginBottom: '16px', fontSize: '16px', fontWeight: 600 }}>活动列表</h4>
          <div className="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>活动编码</th>
                  <th>活动名称</th>
                  <th>折扣类型</th>
                  <th>折扣值</th>
                  <th>状态</th>
                </tr>
              </thead>
              <tbody>
                {promotions.map((p) => (
                  <tr key={p.code}>
                    <td><strong>{p.code}</strong></td>
                    <td>{p.name}</td>
                    <td>
                      <span className="badge badge-primary">
                        {p.discountType === 'percentage' ? '百分比' : '固定金额'}
                      </span>
                    </td>
                    <td>{p.discountValue}{p.discountType === 'percentage' ? '%' : '元'}</td>
                    <td>
                      <span className={`badge ${p.isEnabled ? 'badge-success' : 'badge-error'}`}>
                        {p.isEnabled ? '启用' : '停用'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {promotions.length === 0 && !loading && (
        <div className="empty-state" style={{ marginTop: '24px' }}>
          <div className="empty-state-icon">🎯</div>
          <div className="empty-state-text">暂无营销活动</div>
        </div>
      )}
    </div>
  )
}

export function FinancePage() {
  const [runs, setRuns] = useState<Array<{ id: string; jobName: string; status: string; message: string }>>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const loadRuns = async () => {
    try {
      setLoading(true)
      const q = new URLSearchParams({ page: '1', pageSize: '20' })
      const result = await api.listJobRuns(q)
      setRuns(result.items)
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadRuns()
  }, [])

  const run = async (name: 'reconcile-refunds' | 'expire-awaiting-payments' | 'reclaim-expired-inventory-reservations' | 'apply-catalog-shelf-schedules') => {
    try {
      setError('')
      await api.runJob(name)
      recordAudit('finance.job.run', name)
      await loadRuns()
    } catch (e) {
      setError(e instanceof Error ? e.message : '执行失败')
    }
  }

  const jobLabels: Record<string, string> = {
    'reconcile-refunds': '退款对账',
    'expire-awaiting-payments': '超时关单',
    'reclaim-expired-inventory-reservations': '库存回收',
    'apply-catalog-shelf-schedules': '类目上架调度',
  }

  return (
    <div className="card">
      <h3 className="card-title">财务结算与对账</h3>
      <p className="card-subtitle">执行财务相关任务和查看执行记录</p>

      {error && <div className="message message-error">⚠️ {error}</div>}

      {/* 操作按钮 */}
      <div className="grid grid-3">
        <button
          type="button"
          className="btn btn-primary btn-lg"
          onClick={() => void run('reconcile-refunds')}
        >
          💳 退款对账
        </button>
        <button
          type="button"
          className="btn btn-warning btn-lg"
          onClick={() => void run('expire-awaiting-payments')}
        >
          ⏰ 超时关单
        </button>
        <button
          type="button"
          className="btn btn-ghost btn-lg"
          onClick={() => void run('reclaim-expired-inventory-reservations')}
        >
          📦 库存回收
        </button>
      </div>

      {/* 执行记录 */}
      {runs.length > 0 && (
        <>
          <h4 style={{ marginTop: '24px', marginBottom: '16px', fontSize: '16px', fontWeight: 600 }}>执行记录</h4>
          <div className="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>任务ID</th>
                  <th>任务名称</th>
                  <th>状态</th>
                  <th>消息</th>
                </tr>
              </thead>
              <tbody>
                {runs.map((r) => (
                  <tr key={r.id}>
                    <td className="text-tertiary">{r.id}</td>
                    <td><strong>{jobLabels[r.jobName] || r.jobName}</strong></td>
                    <td>
                      <span className={`badge ${r.status === 'Success' ? 'badge-success' : r.status === 'Running' ? 'badge-warning' : 'badge-error'}`}>
                        {r.status}
                      </span>
                    </td>
                    <td className="text-secondary">{r.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {runs.length === 0 && !loading && (
        <div className="empty-state" style={{ marginTop: '24px' }}>
          <div className="empty-state-icon">💰</div>
          <div className="empty-state-text">暂无执行记录</div>
        </div>
      )}
    </div>
  )
}

export function TicketPage() {
  const [tickets, setTickets] = useState([
    { id: 'tk-001', title: '用户投诉商品与描述不符', status: 'open', priority: 'high' },
    { id: 'tk-002', title: '商家发货延迟申诉', status: 'processing', priority: 'medium' },
    { id: 'tk-003', title: '退款申请未处理', status: 'open', priority: 'high' },
    { id: 'tk-004', title: '商品质量问题反馈', status: 'resolved', priority: 'low' },
  ])

  const update = (id: string, status: string) => {
    setTickets((prev) => prev.map((t) => (t.id === id ? { ...t, status } : t)))
    recordAudit('ticket.status.updated', `${id}:${status}`)
  }

  const priorityMap: Record<string, { label: string; badge: string }> = {
    high: { label: '高', badge: 'badge-error' },
    medium: { label: '中', badge: 'badge-warning' },
    low: { label: '低', badge: 'badge-primary' },
  }

  const statusMap: Record<string, { label: string; badge: string }> = {
    open: { label: '待处理', badge: 'badge-error' },
    processing: { label: '处理中', badge: 'badge-warning' },
    resolved: { label: '已解决', badge: 'badge-success' },
  }

  return (
    <div className="card">
      <h3 className="card-title">客服工单</h3>
      <p className="card-subtitle">处理和跟踪用户客服工单</p>

      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>工单ID</th>
              <th>工单标题</th>
              <th>优先级</th>
              <th>状态</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            {tickets.map((t) => (
              <tr key={t.id}>
                <td className="text-tertiary">{t.id}</td>
                <td><strong>{t.title}</strong></td>
                <td>
                  <span className={`badge ${priorityMap[t.priority].badge}`}>
                    {priorityMap[t.priority].label}
                  </span>
                </td>
                <td>
                  <span className={`badge ${statusMap[t.status].badge}`}>
                    {statusMap[t.status].label}
                  </span>
                </td>
                <td>
                  <div className="action-group">
                    {t.status === 'open' && (
                      <button
                        type="button"
                        className="btn btn-sm btn-warning"
                        onClick={() => update(t.id, 'processing')}
                      >
                        开始处理
                      </button>
                    )}
                    {t.status === 'processing' && (
                      <button
                        type="button"
                        className="btn btn-sm btn-success"
                        onClick={() => update(t.id, 'resolved')}
                      >
                        标记解决
                      </button>
                    )}
                    {t.status === 'resolved' && (
                      <span className="text-success">✓ 已关闭</span>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {tickets.length === 0 && (
        <div className="empty-state">
          <div className="empty-state-icon">🎫</div>
          <div className="empty-state-text">暂无客服工单</div>
        </div>
      )}
    </div>
  )
}

export function RiskPage() {
  const [words, setWords] = useState('违禁词1,违禁词2')
  const [ruleEnabled, setRuleEnabled] = useState(true)
  const [alerts, setAlerts] = useState<string[]>([])
  const [success, setSuccess] = useState('')

  const save = () => {
    localStorage.setItem('platform_risk_words', words)
    localStorage.setItem('platform_risk_rule_enabled', ruleEnabled ? '1' : '0')
    recordAudit('risk.config.updated', `enabled=${ruleEnabled};words=${words}`)
    setAlerts((prev) => [`${new Date().toISOString()} saved risk config`, ...prev].slice(0, 20))
    setSuccess('风控配置保存成功！')
    setTimeout(() => setSuccess(''), 3000)
  }

  return (
    <div className="card">
      <h3 className="card-title">风控与安全</h3>
      <p className="card-subtitle">配置平台风控策略和安全规则</p>

      {success && <div className="message message-success">✓ {success}</div>}

      <div className="form-group">
        <label className="form-label">敏感词库</label>
        <input
          className="form-input"
          value={words}
          onChange={(e) => setWords(e.target.value)}
          placeholder="输入敏感词，用逗号分隔"
        />
        <p className="text-tertiary" style={{ marginTop: '8px', fontSize: '12px' }}>
          提示：多个敏感词请用逗号分隔，例如：违禁词1,违禁词2,违禁词3
        </p>
      </div>

      <div className="form-group">
        <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={ruleEnabled}
            onChange={(e) => setRuleEnabled(e.target.checked)}
            style={{ width: '18px', height: '18px', cursor: 'pointer' }}
          />
          <span style={{ fontSize: '14px', fontWeight: 500 }}>启用反刷策略</span>
        </label>
        <p className="text-tertiary" style={{ marginTop: '8px', fontSize: '12px' }}>
          开启后将自动检测和拦截异常刷单行为
        </p>
      </div>

      <button
        type="button"
        className="btn btn-primary"
        onClick={save}
      >
        💾 保存风控配置
      </button>

      {/* 操作日志 */}
      {alerts.length > 0 && (
        <>
          <h4 style={{ marginTop: '24px', marginBottom: '16px', fontSize: '16px', fontWeight: 600 }}>操作日志</h4>
          <div style={{ 
            maxHeight: '300px', 
            overflowY: 'auto', 
            background: '#f5f5f5', 
            borderRadius: '8px', 
            padding: '12px' 
          }}>
            {alerts.map((a, index) => (
              <div
                key={index}
                style={{ 
                  padding: '8px', 
                  marginBottom: '8px', 
                  background: '#fff', 
                  borderRadius: '4px',
                  fontSize: '12px',
                  fontFamily: 'monospace'
                }}
              >
                {a}
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  )
}

export function SystemPage() {
  const [feeRate, setFeeRate] = useState('0.03')
  const [paymentTimeout, setPaymentTimeout] = useState('30')
  const [regionVersion, setRegionVersion] = useState('cn-mainland-v1')
  const [success, setSuccess] = useState('')

  const save = () => {
    localStorage.setItem('platform_system_fee_rate', feeRate)
    localStorage.setItem('platform_system_payment_timeout', paymentTimeout)
    localStorage.setItem('platform_system_region_version', regionVersion)
    recordAudit('system.config.updated', `fee=${feeRate};timeout=${paymentTimeout};region=${regionVersion}`)
    setSuccess('系统参数保存成功！')
    setTimeout(() => setSuccess(''), 3000)
  }

  return (
    <div className="card">
      <h3 className="card-title">系统设置</h3>
      <p className="card-subtitle">配置平台核心系统参数</p>

      {success && <div className="message message-success">✓ {success}</div>}

      <div className="form-group">
        <label className="form-label">手续费率</label>
        <input
          className="form-input"
          value={feeRate}
          onChange={(e) => setFeeRate(e.target.value)}
          placeholder="0.03"
        />
        <p className="text-tertiary" style={{ marginTop: '8px', fontSize: '12px' }}>
          平台收取的交易手续费比例，例如：0.03 表示 3%
        </p>
      </div>

      <div className="form-group">
        <label className="form-label">支付超时（分钟）</label>
        <input
          className="form-input"
          type="number"
          value={paymentTimeout}
          onChange={(e) => setPaymentTimeout(e.target.value)}
          placeholder="30"
        />
        <p className="text-tertiary" style={{ marginTop: '8px', fontSize: '12px' }}>
          用户下单后的支付超时时间，超时后订单自动关闭
        </p>
      </div>

      <div className="form-group">
        <label className="form-label">地区数据版本</label>
        <input
          className="form-input"
          value={regionVersion}
          onChange={(e) => setRegionVersion(e.target.value)}
          placeholder="cn-mainland-v1"
        />
        <p className="text-tertiary" style={{ marginTop: '8px', fontSize: '12px' }}>
          当前使用的地区数据版本标识
        </p>
      </div>

      <button
        type="button"
        className="btn btn-primary"
        onClick={save}
      >
        💾 保存系统参数
      </button>
    </div>
  )
}
export function AuditPage() {
  const rows = listAudit()

  return (
    <div className="card">
      <h3 className="card-title">操作审计（前端埋点）</h3>
      <p className="card-subtitle">记录所有平台管理操作日志</p>

      {rows.length === 0 ? (
        <div className="empty-state">
          <div className="empty-state-icon">📝</div>
          <div className="empty-state-text">暂无操作记录</div>
        </div>
      ) : (
        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>时间</th>
                <th>操作类型</th>
                <th>详情</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={`${r.at}-${i}`}>
                  <td className="text-tertiary">{r.at}</td>
                  <td>
                    <span className="badge badge-primary">{r.action}</span>
                  </td>
                  <td className="text-secondary">{r.detail}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
