import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from '../shared/api'
import { getShopId } from '../shared/auth'
import { recordAudit } from '../shared/audit'
import type { CatalogItem } from '../types'

export function ProductPage() {
  const [items, setItems] = useState<CatalogItem[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [name, setName] = useState('')
  const [price, setPrice] = useState('99')
  const [categoryId, setCategoryId] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const shopId = getShopId()

  const load = useCallback(async () => {
    try {
      setError('')
      const q = new URLSearchParams({ page: '1', pageSize: '50', shopId })
      const res = await api.listCatalogItems(q)
      setItems(res.items.filter((x) => !x.shopId || x.shopId === shopId))
      if (!selectedId && res.items.length > 0) {
        setSelectedId(res.items[0].id)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    }
  }, [selectedId, shopId])

  useEffect(() => {
    void load()
  }, [load])

  const selected = useMemo(() => items.find((x) => x.id === selectedId) ?? null, [items, selectedId])

  useEffect(() => {
    if (!selected) return
    setName(selected.name)
    setPrice(String(selected.price ?? 99))
    setCategoryId(selected.categoryId ?? '')
  }, [selected])

  const saveDraft = async () => {
    if (!selected) return
    try {
      setError('')
      setMessage('')
      await api.updateCatalogItem(selected.id, {
        id: selected.id,
        name: name.trim(),
        shopId: selected.shopId || shopId,
        categoryId: categoryId.trim() || undefined,
        price: Number(price),
      })
      recordAudit('merchant.product.updated', selected.id)
      setMessage('保存成功')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '保存失败')
    }
  }

  const submitAudit = async () => {
    if (!selected) return
    try {
      setError('')
      setMessage('')
      await api.submitCatalogItem(selected.id, note.trim() || undefined)
      recordAudit('merchant.product.submitted', selected.id)
      setMessage('提审成功')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '提审失败')
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">商品管理</h1>
        <p className="page-description">管理您的商品信息，支持草稿编辑和提交审核</p>
      </div>

      {error && (
        <div className="message message-error">
          <span>⚠️</span>
          <span>{error}</span>
        </div>
      )}

      {message && (
        <div className="message message-success">
          <span>✅</span>
          <span>{message}</span>
        </div>
      )}

      <div className="grid grid-2">
        {/* 商品列表 */}
        <div className="card">
          <div className="card-header">
            <h3 className="card-title">商品列表</h3>
            <div className="card-extra">
              <button className="btn btn-sm" onClick={() => void load()}>
                🔄 刷新
              </button>
            </div>
          </div>
          <div className="card-body">
            {items.length === 0 ? (
              <div className="empty-state">
                <div className="empty-icon">📦</div>
                <p className="empty-text">暂无商品</p>
              </div>
            ) : (
              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>商品名称</th>
                      <th>状态</th>
                      <th>操作</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((item) => (
                      <tr
                        key={item.id}
                        style={{
                          background: selectedId === item.id ? 'var(--bg-active)' : 'transparent',
                          cursor: 'pointer',
                        }}
                        onClick={() => setSelectedId(item.id)}
                      >
                        <td>{item.name}</td>
                        <td>
                          <span className={`badge ${
                            item.auditStatus === 'approved' ? 'badge-success' :
                            item.auditStatus === 'rejected' ? 'badge-error' :
                            'badge-warning'
                          }`}>
                            {item.auditStatus === 'approved' ? '已通过' :
                             item.auditStatus === 'rejected' ? '已拒绝' :
                             item.auditStatus === 'pending' ? '审核中' : '草稿'}
                          </span>
                        </td>
                        <td>
                          <button className="btn btn-sm btn-primary">编辑</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>

        {/* 编辑表单 */}
        <div className="card">
          <div className="card-header">
            <h3 className="card-title">
              {selected ? '编辑商品' : '请选择商品'}
            </h3>
          </div>
          <div className="card-body">
            {selected ? (
              <div className="form-group">
                <div className="form-group">
                  <label className="form-label form-label-required">商品名称</label>
                  <input
                    className="form-input"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="请输入商品名称"
                  />
                </div>

                <div className="form-group">
                  <label className="form-label form-label-required">价格</label>
                  <input
                    className="form-input"
                    value={price}
                    onChange={(e) => setPrice(e.target.value)}
                    placeholder="请输入价格"
                    type="number"
                  />
                </div>

                <div className="form-group">
                  <label className="form-label">类目ID</label>
                  <input
                    className="form-input"
                    value={categoryId}
                    onChange={(e) => setCategoryId(e.target.value)}
                    placeholder="请输入类目ID（可选）"
                  />
                </div>

                <div className="form-group">
                  <label className="form-label">提审备注</label>
                  <textarea
                    className="form-textarea"
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                    placeholder="请输入提审备注（可选）"
                  />
                </div>

                <div className="toolbar" style={{ marginTop: '24px' }}>
                  <button className="btn btn-primary" onClick={() => void saveDraft()}>
                    💾 保存草稿
                  </button>
                  <button className="btn btn-success" onClick={() => void submitAudit()}>
                    📤 提交审核
                  </button>
                </div>
              </div>
            ) : (
              <div className="empty-state">
                <div className="empty-icon">👈</div>
                <p className="empty-text">请从左侧选择要编辑的商品</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
