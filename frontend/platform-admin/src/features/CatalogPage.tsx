import { useEffect, useState } from 'react'
import { api } from '../shared/api'
import type { CatalogCategoryNode } from '../types'

function flatten(nodes: CatalogCategoryNode[], depth = 0): Array<{ id: string; name: string; depth: number; enabled: boolean }> {
  const out: Array<{ id: string; name: string; depth: number; enabled: boolean }> = []
  for (const n of nodes) {
    out.push({ id: n.id, name: n.name, depth, enabled: n.isEnabled })
    out.push(...flatten(n.children, depth + 1))
  }
  return out
}

export function CatalogPage() {
  const [rows, setRows] = useState<Array<{ id: string; name: string; depth: number; enabled: boolean }>>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const run = async () => {
      try {
        setRows(flatten(await api.listCategories()))
      } catch (e) {
        setError(e instanceof Error ? e.message : '加载失败')
      } finally {
        setLoading(false)
      }
    }
    void run()
  }, [])

  return (
    <div className="card">
      <h3 className="card-title">类目与属性管理</h3>
      <p className="card-subtitle">管理平台商品类目结构和属性模板</p>

      {loading && (
        <div className="loading">
          <div className="spinner"></div>
          <span style={{ marginLeft: '12px' }}>加载中...</span>
        </div>
      )}

      {error && <div className="message message-error">⚠️ {error}</div>}

      {!loading && !error && (
        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>类目名称</th>
                <th>层级</th>
                <th>状态</th>
                <th>类目ID</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id}>
                  <td style={{ paddingLeft: `${r.depth * 24 + 16}px` }}>
                    {r.depth > 0 && <span style={{ color: 'var(--text-quaternary)', marginRight: '8px' }}>{'─'.repeat(r.depth)}</span>}
                    <strong>{r.name}</strong>
                  </td>
                  <td>
                    <span className="badge badge-primary">L{r.depth + 1}</span>
                  </td>
                  <td>
                    <span className={`badge ${r.enabled ? 'badge-success' : 'badge-error'}`}>
                      {r.enabled ? '启用' : '停用'}
                    </span>
                  </td>
                  <td className="text-tertiary">{r.id}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {rows.length === 0 && !loading && !error && (
        <div className="empty-state">
          <div className="empty-state-icon">📂</div>
          <div className="empty-state-text">暂无类目数据</div>
        </div>
      )}
    </div>
  )
}
