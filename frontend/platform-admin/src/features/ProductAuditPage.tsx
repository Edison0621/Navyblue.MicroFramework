import { useEffect, useState } from 'react'
import { api } from '../shared/api'
import type { CatalogItem } from '../types'
import { recordAudit } from '../shared/audit'

export function ProductAuditPage() {
  const [items, setItems] = useState<CatalogItem[]>([])
  const [loading, setLoading] = useState(true)

  const load = async () => {
    try {
      setLoading(true)
      const query = new URLSearchParams({ page: '1', pageSize: '20', auditStatus: 'PendingReview' })
      const res = await api.listCatalogItems(query)
      setItems(res.items)
    } catch (e) {
      console.error('Failed to load items:', e)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [])

  const handleApprove = async (id: string, _name: string) => {
    await api.approveItem(id, 'approved by platform')
    recordAudit('catalog.item.approved', id)
    await load()
  }

  const handleReject = async (id: string, _name: string) => {
    await api.rejectItem(id, '违规词命中', 'reject by platform')
    recordAudit('catalog.item.rejected', id)
    await load()
  }

  return (
    <div className="card">
      <h3 className="card-title">商品审核</h3>
      <p className="card-subtitle">审核商家提交的商品，确保符合平台规范</p>

      {loading && (
        <div className="loading">
          <div className="spinner"></div>
          <span style={{ marginLeft: '12px' }}>加载中...</span>
        </div>
      )}

      {!loading && (
        <>
          {items.length > 0 && (
            <div className="table-wrapper">
              <table>
                <thead>
                  <tr>
                    <th>商品名称</th>
                    <th>商家ID</th>
                    <th>商品ID</th>
                    <th>操作</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((x) => (
                    <tr key={x.id}>
                      <td><strong>{x.name}</strong></td>
                      <td className="text-tertiary">{x.shopId}</td>
                      <td className="text-tertiary">{x.id}</td>
                      <td>
                        <div className="action-group">
                          <button
                            type="button"
                            className="btn btn-sm btn-success"
                            onClick={() => void handleApprove(x.id, x.name)}
                          >
                            ✓ 通过
                          </button>
                          <button
                            type="button"
                            className="btn btn-sm btn-error"
                            onClick={() => void handleReject(x.id, x.name)}
                          >
                            ✗ 驳回
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {items.length === 0 && (
            <div className="empty-state">
              <div className="empty-state-icon">✅</div>
              <div className="empty-state-text">暂无待审核商品</div>
            </div>
          )}
        </>
      )}
    </div>
  )
}
