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
      await api.updateCatalogItem(selected.id, {
        id: selected.id,
        name: name.trim(),
        shopId: selected.shopId || shopId,
        categoryId: categoryId.trim() || undefined,
        price: Number(price),
      })
      recordAudit('merchant.product.updated', selected.id)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '保存失败')
    }
  }

  const submitAudit = async () => {
    if (!selected) return
    try {
      setError('')
      await api.submitCatalogItem(selected.id, note.trim() || undefined)
      recordAudit('merchant.product.submitted', selected.id)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '提审失败')
    }
  }

  return (
    <section className="card">
      <h3>商品管理</h3>
      <p className="muted">支持草稿编辑、提交审核；审核结论由平台端处理。</p>
      {error && <p className="error">{error}</p>}
      <div className="row">
        <button type="button" onClick={() => void load()}>刷新</button>
        <select value={selectedId} onChange={(e) => setSelectedId(e.target.value)}>
          <option value="">请选择商品</option>
          {items.map((x) => (
            <option key={x.id} value={x.id}>{x.name} ({x.auditStatus ?? 'draft'})</option>
          ))}
        </select>
      </div>
      {selected ? (
        <>
          <div className="row">
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder="商品名称" />
            <input value={price} onChange={(e) => setPrice(e.target.value)} placeholder="价格" />
            <input value={categoryId} onChange={(e) => setCategoryId(e.target.value)} placeholder="类目ID" />
          </div>
          <div className="row">
            <textarea value={note} onChange={(e) => setNote(e.target.value)} placeholder="提审备注（可选）" />
          </div>
          <div className="row">
            <button type="button" onClick={() => void saveDraft()}>保存草稿</button>
            <button type="button" onClick={() => void submitAudit()}>提交审核</button>
          </div>
        </>
      ) : (
        <p className="muted">暂无可操作商品。</p>
      )}
    </section>
  )
}
