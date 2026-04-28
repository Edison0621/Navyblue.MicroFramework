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
  const [loading, setLoading] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const query = new URLSearchParams({ page: '1', pageSize: '30' })
      if (q) query.set('q', q)
      const res = await api.listUsers(query)
      setUsers(res.items)
      if (selected) setSelected(res.items.find((x) => x.id === selected.id) ?? null)
    } catch (e) {
      console.error('Failed to load users:', e)
    } finally {
      setLoading(false)
    }
  }, [q, selected])

  useEffect(() => { void load() }, [load])

  const closePool = useMemo(() => users.filter((u) => u.closeRequest?.status === 'pending'), [users])

  return (
    <>
      {/* 用户列表 */}
      <div className="card">
        <h3 className="card-title">用户管理</h3>
        <p className="card-subtitle">管理平台用户，包括标签、等级、黑名单等</p>

        {/* 搜索栏 */}
        <div className="toolbar">
          <div className="toolbar-left">
            <div className="form-group" style={{ marginBottom: 0, minWidth: '300px' }}>
              <input
                className="form-input"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="搜索用户名/邮箱"
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
                  <span>搜索中...</span>
                </>
              ) : (
                '搜索'
              )}
            </button>
          </div>
        </div>

        {/* 用户表格 */}
        {users.length > 0 && (
          <div className="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th style={{ width: '40px' }}>
                    <input
                      type="checkbox"
                      checked={batchIds.length === users.length && users.length > 0}
                      onChange={(e) => {
                        if (e.target.checked) {
                          setBatchIds(users.map(u => u.id))
                        } else {
                          setBatchIds([])
                        }
                      }}
                    />
                  </th>
                  <th>用户名</th>
                  <th>邮箱</th>
                  <th>等级</th>
                  <th>状态</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <input
                        type="checkbox"
                        checked={batchIds.includes(u.id)}
                        onChange={(e) => setBatchIds((prev) => e.target.checked ? [...prev, u.id] : prev.filter((id) => id !== u.id))}
                      />
                    </td>
                    <td><strong>{u.username}</strong></td>
                    <td className="text-tertiary">{maskEmail(u.email)}</td>
                    <td>
                      <span className={`badge ${u.level === 'vip' ? 'badge-warning' : 'badge-primary'}`}>
                        {u.level === 'vip' ? 'VIP' : '普通'}
                      </span>
                    </td>
                    <td>
                      <span className={`badge ${u.blacklist.isBlacklisted ? 'badge-error' : 'badge-success'}`}>
                        {u.blacklist.isBlacklisted ? '已拉黑' : '正常'}
                      </span>
                    </td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-sm btn-ghost"
                        onClick={() => setSelected(u)}
                      >
                        详情
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* 批量操作 */}
        {batchIds.length > 0 && (
          <div className="message message-warning">
            <span>已选择 {batchIds.length} 个用户</span>
          </div>
        )}
        <div className="action-group" style={{ marginTop: '16px' }}>
          <button
            type="button"
            className="btn btn-warning"
            disabled={batchIds.length === 0}
            onClick={() => void api.batchPatchLevel(batchIds, 'vip', undefined, undefined, 'batch vip').then(() => load())}
          >
            批量升为 VIP
          </button>
          <button
            type="button"
            className="btn btn-error"
            disabled={batchIds.length === 0}
            onClick={() => void api.batchPatchBlacklist(batchIds, true, 'batch-risk').then(() => load())}
          >
            批量拉黑
          </button>
        </div>

        {users.length === 0 && !loading && (
          <div className="empty-state">
            <div className="empty-state-icon">👥</div>
            <div className="empty-state-text">暂无用户数据</div>
          </div>
        )}
      </div>

      {/* 用户详情 */}
      {selected && (
        <div className="card">
          <h3 className="card-title">用户详情：{selected.username}</h3>
          
          <div className="form-group">
            <label className="form-label">用户标签</label>
            <p className="text-secondary">{selected.tags.join(', ') || '暂无标签'}</p>
          </div>

          <div className="form-group">
            <label className="form-label">新增标签</label>
            <div className="action-group">
              <input
                className="form-input"
                style={{ flex: 1 }}
                value={newTag}
                onChange={(e) => setNewTag(e.target.value)}
                placeholder="输入新标签"
              />
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => void api.patchTags(selected.id, [...selected.tags, newTag], 'platform update').then(() => { 
                  recordAudit('user.tags.updated', selected.id)
                  setNewTag('')
                  load()
                })}
              >
                更新标签
              </button>
            </div>
          </div>

          <div className="action-group">
            <button
              type="button"
              className={`btn ${selected.blacklist.isBlacklisted ? 'btn-success' : 'btn-error'}`}
              onClick={() => void api.patchBlacklist(selected.id, !selected.blacklist.isBlacklisted, 'risk-control').then(() => { 
                recordAudit('user.blacklist.updated', selected.id)
                load()
              })}
            >
              {selected.blacklist.isBlacklisted ? '解封用户' : '拉黑用户'}
            </button>
            <button
              type="button"
              className="btn btn-warning"
              onClick={() => void api.patchLevel(selected.id, selected.level === 'vip' ? 'normal' : 'vip').then(() => { 
                recordAudit('user.level.updated', selected.id)
                load()
              })}
            >
              调整等级 ({selected.level === 'vip' ? '降为普通' : '升为VIP'})
            </button>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => setSelected(null)}
            >
              关闭详情
            </button>
          </div>
        </div>
      )}

      {/* 注销审批 */}
      <div className="card">
        <h3 className="card-title">注销审批池</h3>
        <p className="card-subtitle">处理用户提交的账号注销申请</p>

        {closePool.length === 0 ? (
          <div className="empty-state">
            <div className="empty-state-icon">✅</div>
            <div className="empty-state-text">暂无待审批的注销申请</div>
          </div>
        ) : (
          <div className="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>用户名</th>
                  <th>注销原因</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {closePool.map((u) => (
                  <tr key={u.id}>
                    <td><strong>{u.username}</strong></td>
                    <td>{u.closeRequest?.reason ?? '-'}</td>
                    <td>
                      <div className="action-group">
                        <button
                          type="button"
                          className="btn btn-sm btn-success"
                          onClick={() => void api.reviewClose(u.id, 'approve', 'approved by admin').then(() => { 
                            recordAudit('user.close.approved', u.id)
                            load()
                          })}
                        >
                          ✓ 通过
                        </button>
                        <button
                          type="button"
                          className="btn btn-sm btn-error"
                          onClick={() => void api.reviewClose(u.id, 'reject', 'rejected by admin').then(() => { 
                            recordAudit('user.close.rejected', u.id)
                            load()
                          })}
                        >
                          ✗ 拒绝
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  )
}
