import { useEffect, useState } from 'react'
import { api } from '../shared/api'
import type { CatalogItem } from '../types'
import { recordAudit } from '../shared/audit'

export function ProductAuditPage() {
  const [items, setItems] = useState<CatalogItem[]>([])
  const load = async () => {
    const query = new URLSearchParams({ page: '1', pageSize: '20', auditStatus: 'PendingReview' })
    const res = await api.listCatalogItems(query)
    setItems(res.items)
  }
  useEffect(() => { void load() }, [])

  return (
    <section className="card">
      <h3>商品审核</h3>
      {items.map((x) => (
        <div className="row" key={x.id}>
          <span>{x.name}</span>
          <span>{x.shopId}</span>
          <button type="button" onClick={() => void api.approveItem(x.id, 'approved by platform').then(() => { recordAudit('catalog.item.approved', x.id); load() })}>通过</button>
          <button type="button" onClick={() => void api.rejectItem(x.id, '违规词命中', 'reject by platform').then(() => { recordAudit('catalog.item.rejected', x.id); load() })}>驳回</button>
        </div>
      ))}
    </section>
  )
}
