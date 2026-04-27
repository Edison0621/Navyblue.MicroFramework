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
  useEffect(() => {
    const run = async () => {
      try {
        setRows(flatten(await api.listCategories()))
      } catch (e) {
        setError(e instanceof Error ? e.message : '加载失败')
      }
    }
    void run()
  }, [])
  return (
    <section className="card">
      <h3>类目与属性管理</h3>
      {error && <p className="error">{error}</p>}
      {rows.map((r) => (
        <p key={r.id}>{'--'.repeat(r.depth)} {r.name} ({r.enabled ? '启用' : '停用'})</p>
      ))}
    </section>
  )
}
