import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from '../shared/api'
import type { BuyerUser } from '../types'
import { recordAudit } from '../shared/audit'

function maskEmail(email: string) {
  const [name, domain] = email.split('@')
  if (!domain || name.length < 2) return email
  return `${name[0]}***${name[name.length - 1]}@${domain}`
}

export function UserGovernancePage() {
  const [q, setQ] = useState('')
  const [users, setUsers] = useState<BuyerUser[]>([])
  const [selected, setSelected] = useState<BuyerUser | null>(null)
  const [newTag, setNewTag] = useState('')
  const [batchIds, setBatchIds] = useState<string[]>([])

  const load = useCallback(async () => {
    const query = new URLSearchParams({ page: '1', pageSize: '30' })
    if (q) query.set('q', q)
    const res = await api.listUsers(query)
    setUsers(res.items)
    if (selected) setSelected(res.items.find((x) => x.id === selected.id) ?? null)
  }, [q, selected])
  useEffect(() => { void load() }, [load])

  const closePool = useMemo(() => users.filter((u) => u.closeRequest?.status === 'pending'), [users])

  return (
    <>
      <section className="card">
        <h3>用户管理</h3>
        <div className="row">
          <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="搜索用户名/邮箱" />
          <button type="button" onClick={() => void load()}>搜索</button>
        </div>
        {users.map((u) => (
          <div className="row" key={u.id}>
            <input
              type="checkbox"
              checked={batchIds.includes(u.id)}
              onChange={(e) => setBatchIds((prev) => e.target.checked ? [...prev, u.id] : prev.filter((id) => id !== u.id))}
            />
            <button type="button" onClick={() => setSelected(u)}>详情</button>
            <span>{u.username}</span><span>{maskEmail(u.email)}</span><span>{u.level}</span>
            <span>{u.blacklist.isBlacklisted ? '已拉黑' : '正常'}</span>
          </div>
        ))}
        <div className="row">
          <button type="button" disabled={batchIds.length === 0} onClick={() => void api.batchPatchLevel(batchIds, 'vip', undefined, undefined, 'batch vip').then(() => load())}>
            批量升为 VIP
          </button>
          <button type="button" disabled={batchIds.length === 0} onClick={() => void api.batchPatchBlacklist(batchIds, true, 'batch-risk').then(() => load())}>
            批量拉黑
          </button>
        </div>
      </section>
      {selected ? (
        <section className="card">
          <h4>{selected.username}</h4>
          <p>标签：{selected.tags.join(', ') || '-'}</p>
          <div className="row">
            <input value={newTag} onChange={(e) => setNewTag(e.target.value)} placeholder="新增标签" />
            <button type="button" onClick={() => void api.patchTags(selected.id, [...selected.tags, newTag], 'platform update').then(() => { recordAudit('user.tags.updated', selected.id); setNewTag(''); load() })}>更新标签</button>
            <button type="button" onClick={() => void api.patchBlacklist(selected.id, !selected.blacklist.isBlacklisted, 'risk-control').then(() => { recordAudit('user.blacklist.updated', selected.id); load() })}>
              {selected.blacklist.isBlacklisted ? '解封' : '拉黑'}
            </button>
            <button type="button" onClick={() => void api.patchLevel(selected.id, selected.level === 'vip' ? 'normal' : 'vip').then(() => { recordAudit('user.level.updated', selected.id); load() })}>调整等级</button>
          </div>
        </section>
      ) : null}
      <section className="card">
        <h4>注销审批池</h4>
        {closePool.length === 0 ? <p>暂无待审批</p> : null}
        {closePool.map((u) => (
          <div key={u.id} className="row">
            <span>{u.username}</span><span>{u.closeRequest?.reason ?? '-'}</span>
            <button type="button" onClick={() => void api.reviewClose(u.id, 'approve', 'approved by admin').then(() => { recordAudit('user.close.approved', u.id); load() })}>通过</button>
            <button type="button" onClick={() => void api.reviewClose(u.id, 'reject', 'rejected by admin').then(() => { recordAudit('user.close.rejected', u.id); load() })}>拒绝</button>
          </div>
        ))}
      </section>
    </>
  )
}
