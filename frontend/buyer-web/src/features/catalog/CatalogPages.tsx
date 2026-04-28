import { useEffect, useMemo, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useAuth } from '../../app/AuthContext'
import { ApiError, api } from '../../lib/api'
import { getGuestCartLines, setGuestCartLines } from '../../lib/guestCart'
import type { CatalogItem } from '../../types'
import { money } from '../../shared/utils/format'
import { useToast } from '../../shared/ui/ToastProvider'
import { EmptyState, PageHeader, PriceText, StatusPill, SurfaceCard } from '../../shared/ui/Storefront'

const homeCategories = ['家电', '手机数码', '电脑办公', '服饰美妆', '生鲜食品', '家居家装', '母婴童装', '图书文娱']
const guestCatalogSeed: CatalogItem[] = [
  { id: 'guest-1', name: '轻薄笔记本 Pro 14', price: 4999, isActive: true, shopId: 'self-run' },
  { id: 'guest-2', name: '无线降噪耳机', price: 699, isActive: true, shopId: 'digital-life' },
  { id: 'guest-3', name: '65 寸 4K 智能电视', price: 2999, isActive: true, shopId: 'home-appliance' },
  { id: 'guest-4', name: '人体工学办公椅', price: 899, isActive: true, shopId: 'home-office' },
  { id: 'guest-5', name: '筋膜枪旗舰款', price: 459, isActive: true, shopId: 'sports-health' },
  { id: 'guest-6', name: '高支棉四件套', price: 299, isActive: true, shopId: 'home-textile' },
  { id: 'guest-7', name: '便携咖啡机', price: 799, isActive: true, shopId: 'kitchen-life' },
  { id: 'guest-8', name: '儿童学习台灯', price: 169, isActive: true, shopId: 'family-select' },
]

function getGuestCatalog(q: string): CatalogItem[] {
  const keyword = q.trim().toLowerCase()
  if (!keyword) {
    return guestCatalogSeed
  }
  return guestCatalogSeed.filter((item) => item.name.toLowerCase().includes(keyword) || item.shopId.toLowerCase().includes(keyword))
}

export function HomePage() {
  const [items, setItems] = useState<CatalogItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    const run = async () => {
      setLoading(true)
      try {
        const query = new URLSearchParams({ page: '1', pageSize: '8', isActive: 'true' })
        const res = await api.listCatalogItems(query)
        setItems(res.items)
      } catch (err) {
        if (err instanceof ApiError && err.status === 401) {
          setItems(guestCatalogSeed)
          setError('')
        } else {
          setError(err instanceof Error ? err.message : '首页加载失败')
        }
      } finally {
        setLoading(false)
      }
    }
    void run()
  }, [])

  return (
    <section>
      <div className="hero-grid">
        <SurfaceCard>
          <h3>全部分类</h3>
          <ul className="category-list">
            {homeCategories.map((category) => (
              <li key={category}>{category}</li>
            ))}
          </ul>
        </SurfaceCard>
        <SurfaceCard className="promo-banner">
          <h3>京东同款运营节奏</h3>
          <p>限时抢购、跨店满减、爆款直降，今日主推频道已上线。</p>
          <Link to="/products" className="btn btn-primary">
            立即逛商城
          </Link>
        </SurfaceCard>
        <SurfaceCard className="promo-side">
          <h4>新人专享</h4>
          <p className="muted">首单立减、下单送券、会员价折上折。</p>
          <Link to="/cart" className="btn">
            去购物车
          </Link>
        </SurfaceCard>
      </div>
      <PageHeader title="猜你喜欢" subtitle="为你推荐热门在售商品" />
      {loading ? <p className="muted">加载中...</p> : null}
      {error ? <p className="error">{error}</p> : null}
      <div className="product-grid">
        {items.map((item) => (
          <SurfaceCard key={item.id} className="product-card">
            <h3>{item.name}</h3>
            <p className="muted">店铺 {item.shopId}</p>
            <PriceText value={item.price} />
            <Link to={`/products/${item.id}`} className="btn btn-primary">
              查看详情
            </Link>
          </SurfaceCard>
        ))}
      </div>
    </section>
  )
}

export function ProductListPage() {
  const [params, setParams] = useSearchParams()
  const [items, setItems] = useState<CatalogItem[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const q = params.get('q') ?? ''
  const sort = params.get('sort') ?? 'default'
  const { notify } = useToast()
  const { isAuthed } = useAuth()

  useEffect(() => {
    const run = async () => {
      setLoading(true)
      setError('')
      try {
        if (q && isAuthed) {
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
        if (err instanceof ApiError && err.status === 401 && !isAuthed) {
          const next = [...getGuestCatalog(q)]
          if (sort === 'priceAsc') next.sort((a, b) => a.price - b.price)
          if (sort === 'priceDesc') next.sort((a, b) => b.price - a.price)
          setItems(next)
          setError('')
          return
        }
        const message = err instanceof Error ? err.message : '商品加载失败'
        setError(message)
        notify(message, 'error')
      } finally {
        setLoading(false)
      }
    }
    void run()
  }, [q, sort, notify, isAuthed])

  const subtitle = useMemo(() => (q ? `关键词: ${q}` : '为你找到全站在售商品'), [q])

  return (
    <section>
      <PageHeader
        title="商品列表"
        subtitle={subtitle}
        extra={
          !isAuthed ? (
            <StatusPill text="游客模式：可浏览和加购，登录后可下单" />
          ) : (
            <StatusPill text="已登录：支持收藏、搜索记录、下单" />
          )
        }
      />
      <form
        className="toolbar surface-card"
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
      {loading ? <p className="muted">加载中...</p> : null}
      {error && <p className="error">{error}</p>}
      <div className="product-grid">
        {items.map((item) => (
          <SurfaceCard className="product-card" key={item.id}>
            <h3>{item.name}</h3>
            <p className="muted">来自店铺 {item.shopId}</p>
            <PriceText value={item.price} />
            <div className="row">
              <Link className="btn btn-primary" to={`/products/${item.id}`}>
                详情
              </Link>
              <button
                type="button"
                onClick={() => {
                  if (!isAuthed) {
                    notify('登录后可保存收藏', 'info')
                    return
                  }
                  void api
                    .saveFavorite({
                      id: `fav-${item.id}`,
                      type: 'product',
                      targetId: item.id,
                      name: item.name,
                    })
                    .then(() => notify('已收藏', 'success'))
                }}
              >
                收藏
              </button>
            </div>
          </SurfaceCard>
        ))}
      </div>
      {!loading && items.length === 0 ? <EmptyState title="暂无商品" description="换个关键词试试看" actionText="返回首页" actionTo="/" /> : null}
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
  const { isAuthed } = useAuth()

  useEffect(() => {
    const run = async () => {
      try {
        const data = await api.getCatalogItem(id)
        setItem(data)
        if (data.skus?.[0]) {
          setSkuId(data.skus[0].skuId)
        }
        if (isAuthed) {
          await api.saveFootprint({ id: `fp-${Date.now()}`, productId: id, name: data.name, visitedAt: new Date().toISOString() })
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : '加载失败')
      }
    }
    void run()
  }, [id, isAuthed])

  const addToCart = async () => {
    if (!item) return
    const next = isAuthed ? [...(await api.getCart()).lines] : [...getGuestCartLines()]
    const idx = next.findIndex((x) => x.productId === item.id && (x.skuId ?? '') === skuId)
    if (idx >= 0) next[idx] = { ...next[idx], quantity: next[idx].quantity + quantity }
    else next.push({ productId: item.id, skuId: skuId || undefined, quantity })
    if (isAuthed) {
      await api.replaceCart(next.map((x) => ({ productId: x.productId, skuId: x.skuId ?? undefined, quantity: x.quantity })))
      notify('加入购物车成功', 'success')
    } else {
      setGuestCartLines(next)
      notify('已加入购物车，登录后可下单', 'success')
    }
  }

  if (!item) return <p>{error || '加载中...'}</p>
  return (
    <section className="surface-card">
      <PageHeader title={item.name} subtitle={`店铺 ${item.shopId}`} extra={!isAuthed ? <StatusPill text="游客可加购" /> : undefined} />
      <PriceText value={item.price} />
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
      {!isAuthed ? <p className="muted">当前为游客模式，可加购，结算时需登录。</p> : null}
    </section>
  )
}
