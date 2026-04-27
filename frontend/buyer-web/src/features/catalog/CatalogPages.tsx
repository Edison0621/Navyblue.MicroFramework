import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { api } from '../../lib/api'
import type { CatalogItem } from '../../types'
import { money } from '../../shared/utils/format'
import { useToast } from '../../shared/ui/ToastProvider'

export function ProductListPage() {
  const [params, setParams] = useSearchParams()
  const [items, setItems] = useState<CatalogItem[]>([])
  const [error, setError] = useState('')
  const q = params.get('q') ?? ''
  const sort = params.get('sort') ?? 'default'
  const { notify } = useToast()

  useEffect(() => {
    const run = async () => {
      setError('')
      try {
        if (q) {
          await api.saveSearchTerm(q)
        }
        const query = new URLSearchParams({ page: '1', pageSize: '20', isActive: 'true' })
        if (q) query.set('q', q)
        const res = await api.listCatalogItems(query)
        const next = [...res.items]
        if (sort === 'priceAsc') next.sort((a, b) => a.price - b.price)
        if (sort === 'priceDesc') next.sort((a, b) => b.price - a.price)
        setItems(next)
      } catch (err) {
        const message = err instanceof Error ? err.message : '商品加载失败'
        setError(message)
        notify(message, 'error')
      }
    }
    void run()
  }, [q, sort, notify])

  return (
    <section>
      <h2>商品列表</h2>
      <form
        className="row"
        onSubmit={(e) => {
          e.preventDefault()
          const form = new FormData(e.currentTarget)
          const nextQ = String(form.get('q') ?? '')
          setParams({ q: nextQ, sort })
        }}
      >
        <input name="q" defaultValue={q} placeholder="关键词搜索" />
        <select value={sort} onChange={(e) => setParams({ q, sort: e.target.value })}>
          <option value="default">综合</option>
          <option value="priceAsc">价格升序</option>
          <option value="priceDesc">价格降序</option>
        </select>
        <button type="submit">搜索</button>
      </form>
      {error && <p className="error">{error}</p>}
      <div className="grid">
        {items.map((item) => (
          <article className="card" key={item.id}>
            <h3>{item.name}</h3>
            <p>{money(item.price)}</p>
            <div className="row">
              <Link to={`/products/${item.id}`}>详情</Link>
              <button
                type="button"
                onClick={() => {
                  void api.saveFavorite({
                    id: `fav-${item.id}`,
                    type: 'product',
                    targetId: item.id,
                    name: item.name,
                  })
                  notify('已收藏', 'success')
                }}
              >
                收藏
              </button>
            </div>
          </article>
        ))}
      </div>
    </section>
  )
}

export function ProductDetailPage() {
  const { id = '' } = useParams()
  const [item, setItem] = useState<CatalogItem | null>(null)
  const [skuId, setSkuId] = useState('')
  const [quantity, setQuantity] = useState(1)
  const [error, setError] = useState('')
  const { notify } = useToast()

  useEffect(() => {
    const run = async () => {
      try {
        const data = await api.getCatalogItem(id)
        setItem(data)
        if (data.skus?.[0]) {
          setSkuId(data.skus[0].skuId)
        }
        await api.saveFootprint({ id: `fp-${Date.now()}`, productId: id, name: data.name, visitedAt: new Date().toISOString() })
      } catch (err) {
        setError(err instanceof Error ? err.message : '加载失败')
      }
    }
    void run()
  }, [id])

  const addToCart = async () => {
    if (!item) return
    const cart = await api.getCart()
    const next = [...cart.lines]
    const idx = next.findIndex((x) => x.productId === item.id && (x.skuId ?? '') === skuId)
    if (idx >= 0) next[idx] = { ...next[idx], quantity: next[idx].quantity + quantity }
    else next.push({ productId: item.id, skuId: skuId || undefined, quantity })
    await api.replaceCart(next.map((x) => ({ productId: x.productId, skuId: x.skuId ?? undefined, quantity: x.quantity })))
    notify('加入购物车成功', 'success')
  }

  if (!item) return <p>{error || '加载中...'}</p>
  return (
    <section className="card">
      <h2>{item.name}</h2>
      <p>价格: {money(item.price)}</p>
      {item.skus?.length ? (
        <label>
          规格
          <select value={skuId} onChange={(e) => setSkuId(e.target.value)}>
            {item.skus.map((sku) => (
              <option key={sku.skuId} value={sku.skuId}>
                {sku.name} {money(sku.price)}
              </option>
            ))}
          </select>
        </label>
      ) : null}
      <label>
        数量
        <input type="number" min={1} value={quantity} onChange={(e) => setQuantity(Math.max(1, Number(e.target.value) || 1))} />
      </label>
      <button type="button" onClick={() => void addToCart()}>
        加入购物车
      </button>
    </section>
  )
}
