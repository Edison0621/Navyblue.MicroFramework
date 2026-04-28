import { useEffect, useMemo, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useAuth } from '../../app/AuthContext'
import { ApiError, api } from '../../lib/api'
import { getGuestCartLines, setGuestCartLines } from '../../lib/guestCart'
import type { CatalogItem } from '../../types'
import { money } from '../../shared/utils/format'
import { useToast } from '../../shared/ui/ToastProvider'
import { EmptyState, PageHeader, PriceText, StatusPill, SurfaceCard } from '../../shared/ui/Storefront'

const homeCategories = [
  { name: '家电', icon: '🏠', path: '/products?q=家电' },
  { name: '手机数码', icon: '📱', path: '/products?q=手机' },
  { name: '电脑办公', icon: '💻', path: '/products?q=电脑' },
  { name: '服饰美妆', icon: '👗', path: '/products?q=服饰' },
  { name: '生鲜食品', icon: '🍎', path: '/products?q=食品' },
  { name: '家居家装', icon: '🛋️', path: '/products?q=家居' },
  { name: '母婴童装', icon: '👶', path: '/products?q=母婴' },
  { name: '图书文娱', icon: '📚', path: '/products?q=图书' },
]

const bannerSlides = [
  { id: 1, title: '双11狂欢节', subtitle: '满300减50', bg: 'linear-gradient(120deg, #ff6a5f 0%, #e1251b 70%)' },
  { id: 2, title: '品牌秒杀', subtitle: '限时特价', bg: 'linear-gradient(120deg, #667eea 0%, #764ba2 70%)' },
  { id: 3, title: '新品首发', subtitle: '抢先体验', bg: 'linear-gradient(120deg, #f093fb 0%, #f5576c 70%)' },
]

const flashSaleProducts = [
  { id: 'flash-1', name: '智能手表', price: 299, originalPrice: 599, discount: '5折' },
  { id: 'flash-2', name: '蓝牙耳机', price: 99, originalPrice: 299, discount: '3.3折' },
  { id: 'flash-3', name: '充电宝', price: 49, originalPrice: 129, discount: '3.8折' },
  { id: 'flash-4', name: '手机壳', price: 19, originalPrice: 59, discount: '3.2折' },
]

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
  const [currentSlide, setCurrentSlide] = useState(0)

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

  // 轮播图自动切换
  useEffect(() => {
    const timer = setInterval(() => {
      setCurrentSlide((prev) => (prev + 1) % bannerSlides.length)
    }, 4000)
    return () => clearInterval(timer)
  }, [])

  return (
    <section>
      {/* 轮播图区域 */}
      <div className="banner-slider">
        {bannerSlides.map((slide, index) => (
          <div
            key={slide.id}
            className={`banner-slide ${index === currentSlide ? 'active' : ''}`}
            style={{ background: slide.bg }}
          >
            <div className="banner-content">
              <h2>{slide.title}</h2>
              <p>{slide.subtitle}</p>
              <Link to="/products" className="btn btn-white">
                立即抢购
              </Link>
            </div>
          </div>
        ))}
        <div className="banner-dots">
          {bannerSlides.map((_, index) => (
            <span
              key={index}
              className={`dot ${index === currentSlide ? 'active' : ''}`}
              onClick={() => setCurrentSlide(index)}
            />
          ))}
        </div>
      </div>

      {/* 频道导航 */}
      <div className="channel-nav">
        {homeCategories.map((category) => (
          <Link key={category.name} to={category.path} className="channel-item">
            <span className="channel-icon">{category.icon}</span>
            <span className="channel-name">{category.name}</span>
          </Link>
        ))}
      </div>

      {/* 秒杀专区 */}
      <SurfaceCard className="flash-sale-section">
        <div className="section-header">
          <h3>⚡ 限时秒杀</h3>
          <span className="countdown">距结束 02:15:30</span>
        </div>
        <div className="flash-sale-grid">
          {flashSaleProducts.map((product) => (
            <Link key={product.id} to={`/products/${product.id}`} className="flash-sale-item">
              <div className="flash-sale-image">📦</div>
              <h4>{product.name}</h4>
              <div className="flash-sale-price">
                <span className="current-price">{money(product.price)}</span>
                <span className="original-price">{money(product.originalPrice)}</span>
              </div>
              <span className="discount-tag">{product.discount}</span>
            </Link>
          ))}
        </div>
      </SurfaceCard>

      {/* 主内容区 */}
      <div className="home-main-grid">
        <SurfaceCard className="category-card">
          <h3>全部分类</h3>
          <ul className="category-list">
            {homeCategories.map((category) => (
              <li key={category.name}>
                <Link to={category.path}>
                  <span className="category-icon">{category.icon}</span>
                  {category.name}
                </Link>
              </li>
            ))}
          </ul>
        </SurfaceCard>

        <SurfaceCard className="promo-banner">
          <h3>京东同款运营节奏</h3>
          <p>限时抢购、跨店满减、爆款直降，今日主推频道已上线。</p>
          <Link to="/products" className="btn btn-white">
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
            <div className="product-image">📦</div>
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
  const [activeTab, setActiveTab] = useState<'detail' | 'review' | 'service'>('detail')
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

  const selectedSku = item.skus?.find((sku) => sku.skuId === skuId)
  const currentPrice = selectedSku?.price ?? item.price

  return (
    <section>
      <div className="product-detail-grid">
        {/* 左侧图片区 */}
        <SurfaceCard className="product-gallery">
          <div className="main-image">📦</div>
          <div className="thumbnail-list">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="thumbnail">📷</div>
            ))}
          </div>
        </SurfaceCard>

        {/* 右侧信息区 */}
        <SurfaceCard className="product-info">
          <PageHeader
            title={item.name}
            subtitle={`店铺 ${item.shopId}`}
            extra={!isAuthed ? <StatusPill text="游客可加购" /> : undefined}
          />

          <div className="price-section">
            <div className="price-row">
              <span className="price-label">京东价</span>
              <span className="current-price-large">
                <span className="price-symbol-large">￥</span>
                {currentPrice.toFixed(2)}
              </span>
            </div>
            <div className="promotion-tags">
              <span className="tag tag-primary">满199减20</span>
              <span className="tag tag-secondary">新人专享价</span>
              <span className="tag tag-accent">限时促销</span>
            </div>
          </div>

          {item.skus?.length ? (
            <div className="sku-section">
              <label className="sku-label">规格</label>
              <div className="sku-options">
                {item.skus.map((sku) => (
                  <button
                    key={sku.skuId}
                    type="button"
                    className={`sku-btn ${skuId === sku.skuId ? 'active' : ''}`}
                    onClick={() => setSkuId(sku.skuId)}
                  >
                    {sku.name}
                    <span className="sku-price">{money(sku.price)}</span>
                  </button>
                ))}
              </div>
            </div>
          ) : null}

          <div className="quantity-section">
            <label className="sku-label">数量</label>
            <div className="quantity-control">
              <button type="button" onClick={() => setQuantity(Math.max(1, quantity - 1))}>-</button>
              <input
                type="number"
                min={1}
                value={quantity}
                onChange={(e) => setQuantity(Math.max(1, Number(e.target.value) || 1))}
              />
              <button type="button" onClick={() => setQuantity(quantity + 1)}>+</button>
            </div>
          </div>

          <div className="action-buttons">
            <button type="button" className="btn btn-primary btn-large" onClick={() => void addToCart()}>
              加入购物车
            </button>
            <button type="button" className="btn btn-ghost btn-large">
              立即购买
            </button>
            <button
              type="button"
              className="btn btn-icon"
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
              ♡
            </button>
          </div>

          <div className="service-guarantee">
            <h4>服务保障</h4>
            <div className="service-items">
              <span>✓ 正品保证</span>
              <span>✓ 极速退款</span>
              <span>✓ 七天无理由退换</span>
            </div>
          </div>

          {!isAuthed && <p className="muted">当前为游客模式，可加购，结算时需登录。</p>}
        </SurfaceCard>
      </div>

      {/* 详情 Tabs */}
      <SurfaceCard className="product-tabs">
        <div className="tab-header">
          <button
            type="button"
            className={`tab-btn ${activeTab === 'detail' ? 'active' : ''}`}
            onClick={() => setActiveTab('detail')}
          >
            商品详情
          </button>
          <button
            type="button"
            className={`tab-btn ${activeTab === 'review' ? 'active' : ''}`}
            onClick={() => setActiveTab('review')}
          >
            商品评价 (128)
          </button>
          <button
            type="button"
            className={`tab-btn ${activeTab === 'service' ? 'active' : ''}`}
            onClick={() => setActiveTab('service')}
          >
            售后保障
          </button>
        </div>

        <div className="tab-content">
          {activeTab === 'detail' && (
            <div className="detail-content">
              <h3>商品介绍</h3>
              <p>{item.name} - 高品质商品，值得信赖。</p>
              <p>产品特性：</p>
              <ul>
                <li>优质材料，精工制造</li>
                <li>性能卓越，稳定可靠</li>
                <li>售后无忧，质保一年</li>
              </ul>
            </div>
          )}
          {activeTab === 'review' && (
            <div className="review-content">
              <div className="review-summary">
                <div className="rating-overview">
                  <span className="rating-score">4.8</span>
                  <div className="rating-stars">★★★★★</div>
                  <p className="muted">好评率 98%</p>
                </div>
              </div>
              <div className="review-list">
                <div className="review-item">
                  <div className="review-header">
                    <span className="reviewer">用户***1</span>
                    <span className="review-date">2024-01-15</span>
                  </div>
                  <div className="review-stars">★★★★★</div>
                  <p className="review-text">商品质量很好，物流速度快，包装精美，非常满意！</p>
                </div>
                <div className="review-item">
                  <div className="review-header">
                    <span className="reviewer">用户***2</span>
                    <span className="review-date">2024-01-14</span>
                  </div>
                  <div className="review-stars">★★★★★</div>
                  <p className="review-text">性价比超高，推荐购买！</p>
                </div>
              </div>
            </div>
          )}
          {activeTab === 'service' && (
            <div className="service-content">
              <h3>售后服务</h3>
              <ul>
                <li>七天无理由退换货</li>
                <li>质量问题免费退换</li>
                <li>全国联保，享受三包服务</li>
                <li>客服热线：400-xxx-xxxx</li>
              </ul>
            </div>
          )}
        </div>
      </SurfaceCard>
    </section>
  )
}
