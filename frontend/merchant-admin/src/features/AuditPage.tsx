import { listAudit } from '../shared/audit'

export function AuditPage() {
  const rows = listAudit()
  return (
    <section className="card">
      <h3>操作记录（前端埋点）</h3>
      {rows.length === 0 ? <p>暂无记录</p> : null}
      {rows.map((r, i) => (
        <p key={`${r.at}-${i}`}>{r.at} | {r.action} | {r.detail}</p>
      ))}
    </section>
  )
}
