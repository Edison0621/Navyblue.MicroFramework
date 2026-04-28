import { useState } from 'react'
import { recordAudit } from '../shared/audit'

type Merchant = { id: string; name: string; status: 'normal' | 'frozen' | 'closed'; violationScore: number }

const statusMap: Record<Merchant['status'], { label: string; badge: string }> = {
  normal: { label: '正常', badge: 'badge-success' },
  frozen: { label: '冻结', badge: 'badge-warning' },
  closed: { label: '关闭', badge: 'badge-error' },
}

export function MerchantPage() {
  const [items, setItems] = useState<Merchant[]>([
    { id: 'm-100', name: '商家A', status: 'normal', violationScore: 0 },
    { id: 'm-200', name: '商家B', status: 'frozen', violationScore: 8 },
    { id: 'm-300', name: '商家C', status: 'normal', violationScore: 2 },
    { id: 'm-400', name: '商家D', status: 'closed', violationScore: 12 },
  ])

  const setStatus = (id: string, status: Merchant['status']) => {
    setItems((prev) => prev.map((x) => (x.id === id ? { ...x, status } : x)))
    recordAudit('merchant.status.updated', `${id} -> ${status}`)
  }

  return (
    <div className="card">
      <h3 className="card-title">商家管理</h3>
      <p className="card-subtitle">管理平台所有商家，包括状态控制和违规处理</p>

      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>商家ID</th>
              <th>商家名称</th>
              <th>状态</th>
              <th>违规分数</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            {items.map((m) => (
              <tr key={m.id}>
                <td>{m.id}</td>
                <td><strong>{m.name}</strong></td>
                <td>
                  <span className={`badge ${statusMap[m.status].badge}`}>
                    {statusMap[m.status].label}
                  </span>
                </td>
                <td>
                  <span className={`text-${m.violationScore > 10 ? 'error' : m.violationScore > 5 ? 'warning' : 'success'}`}>
                    {m.violationScore}分
                  </span>
                </td>
                <td>
                  <div className="action-group">
                    <button
                      type="button"
                      className={`btn btn-sm ${m.status === 'normal' ? 'btn-primary' : 'btn-ghost'}`}
                      onClick={() => setStatus(m.id, 'normal')}
                    >
                      正常
                    </button>
                    <button
                      type="button"
                      className={`btn btn-sm ${m.status === 'frozen' ? 'btn-warning' : 'btn-ghost'}`}
                      onClick={() => setStatus(m.id, 'frozen')}
                    >
                      冻结
                    </button>
                    <button
                      type="button"
                      className={`btn btn-sm ${m.status === 'closed' ? 'btn-error' : 'btn-ghost'}`}
                      onClick={() => setStatus(m.id, 'closed')}
                    >
                      关闭
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {items.length === 0 && (
        <div className="empty-state">
          <div className="empty-state-icon">🏪</div>
          <div className="empty-state-text">暂无商家数据</div>
        </div>
      )}
    </div>
  )
}
