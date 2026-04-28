import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../app/AuthContext'
import { api } from '../../lib/api'
import { getGuestCartLines, setGuestCartLines } from '../../lib/guestCart'
import type { CartLine, CatalogItem, InvoiceTitle, UserAddress } from '../../types'
import { useToast } from '../../shared/ui/ToastProvider'
import { EmptyState, PageHeader, PriceText, SurfaceCard } from '../../shared/ui/Storefront'

export function CartPage() {
  const [lines, setLines] = useState<CartLine[]>([])
  const [products, setProducts] = useState<Record<string, CatalogItem>>({})
  const [invalid, setInvalid] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [selectedItems, setSelectedItems] = useState<Set<string>>(new Set())
  const navigate = useNavigate()
  const { isAuthed } = useAuth()

  useEffect(() => {
    const run = async () => {
      setLoading(true)
      setError('')
      try {
        const cartLines = isAuthed ? (await api.getCart()).lines : getGuestCartLines()
        setLines(cartLines)
        const dict: Record<string, CatalogItem> = {}
        await Promise.all(
          cartLines.map(async (line) => {
            const item = await api.getCatalogItem(line.productId)
            dict[line.productId] = item
          }),
        )
        setProducts(dict)
        setInvalid(cartLines.filter((x) => !dict[x.productId]?.isActive).map((x) => x.productId))
        // 默认选中所有有效商品
        setSelectedItems(new Set(cartLines.filter((x) => dict[x.productId]?.isActive).map((x) => `${x.productId}-${x.skuId ?? 'na'}`)))
      } catch (err) {
        setError(err instanceof Error ? err.message : '购物车加载失败')
      } finally {
        setLoading(false)
      }
    }
    void run()
  }, [isAuthed])

  // 按店铺分组
  const groupedByShop = useMemo(() => {
    const groups: Record<string, CartLine[]> = {}
    lines.forEach((line) => {
      const shopId = products[line.productId]?.shopId ?? 'unknown'
      if (!groups[shopId]) groups[shopId] = []
      groups[shopId].push(line)
    })
    return groups
  }, [lines, products])

  const total = useMemo(
    () =>
      lines.reduce((sum, line) => {
        const key = `${line.productId}-${line.skuId ?? 'na'}`
        if (!selectedItems.has(key)) return sum
        const item = products[line.productId]
        const skuPrice = item?.skus?.find((sku) => sku.skuId === line.skuId)?.price
        return sum + (skuPrice ?? item?.price ?? 0) * line.quantity
      }, 0),
    [lines, products, selectedItems],
  )

  const save = async (next: CartLine[]) => {
    setLines(next)
    if (isAuthed) {
      await api.replaceCart(next.map((x) => ({ productId: x.productId, skuId: x.skuId ?? undefined, quantity: x.quantity })))
    } else {
      setGuestCartLines(next)
    }
  }

  const toggleSelect = (key: string) => {
    const next = new Set(selectedItems)
    if (next.has(key)) {
      next.delete(key)
    } else {
      next.add(key)
    }
    setSelectedItems(next)
  }

  const selectAll = () => {
    const allKeys = lines
      .filter((x) => !invalid.includes(x.productId))
      .map((x) => `${x.productId}-${x.skuId ?? 'na'}`)
    setSelectedItems(new Set(allKeys))
  }

  const deselectAll = () => {
    setSelectedItems(new Set())
  }

  return (
    <section>
      <PageHeader title="购物车" subtitle={isAuthed ? '已登录购物车' : '游客购物车，登录后可直接下单'} />
      {loading ? <p className="muted">加载中...</p> : null}
      {error ? <p className="error">{error}</p> : null}
      {!loading && !lines.length ? <EmptyState title="购物车还是空的" description="先去挑点心仪商品吧" actionText="去逛逛" actionTo="/products" /> : null}

      {lines.length > 0 && (
        <div className="cart-toolbar surface-card">
          <div className="row">
            <button type="button" onClick={selectAll}>全选</button>
            <button type="button" onClick={deselectAll}>全不选</button>
          </div>
          <p className="muted">已选择 {selectedItems.size} 件商品</p>
        </div>
      )}

      {/* 按店铺分组显示 */}
      {Object.entries(groupedByShop).map(([shopId, shopLines]) => (
        <SurfaceCard key={shopId} className="cart-shop-group">
          <div className="shop-header">
            <h3>店铺: {shopId}</h3>
            <span className="promotion-tag">满199减20</span>
          </div>
          {shopLines.map((line, idx) => {
            const key = `${line.productId}-${line.skuId ?? 'na'}`
            const isSelected = selectedItems.has(key)
            return (
              <div key={key} className={`cart-item ${isSelected ? 'selected' : ''} ${invalid.includes(line.productId) ? 'invalid' : ''}`}>
                <input
                  type="checkbox"
                  checked={isSelected}
                  onChange={() => toggleSelect(key)}
                  disabled={invalid.includes(line.productId)}
                />
                <div className="cart-item-image">📦</div>
                <div className="cart-item-info">
                  <h4>{products[line.productId]?.name ?? line.productId}</h4>
                  {invalid.includes(line.productId) ? <p className="error">商品已失效</p> : null}
                  <p className="muted">规格: 默认</p>
                </div>
                <div className="cart-item-price">
                  <PriceText value={products[line.productId]?.price ?? 0} />
                </div>
                <div className="cart-item-quantity">
                  <button type="button" onClick={() => void save(lines.map((x, i) => (i === lines.indexOf(line) ? { ...x, quantity: Math.max(1, x.quantity - 1) } : x)))}>
                    -
                  </button>
                  <span>{line.quantity}</span>
                  <button type="button" onClick={() => void save(lines.map((x, i) => (i === lines.indexOf(line) ? { ...x, quantity: x.quantity + 1 } : x)))}>
                    +
                  </button>
                </div>
                <button type="button" className="cart-item-delete" onClick={() => void save(lines.filter((_, i) => i !== lines.indexOf(line)))}>
                  删除
                </button>
              </div>
            )
          })}
        </SurfaceCard>
      ))}

      {lines.length ? (
        <SurfaceCard className="cart-summary">
          <div className="summary-row">
            <span className="muted">合计:</span>
            <PriceText value={total} />
          </div>
          <p className="muted">促销: 满199减20, 满399减50</p>
          <button type="button" className="btn btn-primary btn-large" onClick={() => navigate('/checkout')} disabled={selectedItems.size === 0}>
            结算 ({selectedItems.size})
          </button>
        </SurfaceCard>
      ) : null}
    </section>
  )
}

export function CheckoutPage() {
  const [addresses, setAddresses] = useState<UserAddress[]>([])
  const [selectedAddressId, setSelectedAddressId] = useState('')
  const [promoCode, setPromoCode] = useState('')
  const [remark, setRemark] = useState('')
  const [invoiceId, setInvoiceId] = useState('')
  const [invoices, setInvoices] = useState<InvoiceTitle[]>([])
  const [error, setError] = useState('')
  const [orderId, setOrderId] = useState('')
  const navigate = useNavigate()
  const { notify } = useToast()
  const { isAuthed } = useAuth()

  useEffect(() => {
    const run = async () => {
      if (!isAuthed) {
        return
      }
      const list = await api.listMyAddresses()
      setAddresses(list)
      if (list[0]) setSelectedAddressId(list[0].id)
      const inv = await api.listInvoices()
      setInvoices(inv)
      if (inv[0]) setInvoiceId(inv[0].id)
    }
    void run()
  }, [isAuthed])

  const place = async () => {
    if (!isAuthed) {
      setError('请先登录后再下单')
      return
    }
    try {
      const order = await api.checkout(promoCode || null, selectedAddressId)
      setOrderId(order.id)
      notify(`下单成功 ${order.id}`, 'success')
    } catch (err) {
      setError(err instanceof Error ? err.message : '下单失败')
    }
  }

  return (
    <section>
      <PageHeader title="结算台" subtitle="收货、发票、优惠信息确认后提交订单" />
      {!isAuthed ? (
        <SurfaceCard>
          <p>当前为游客模式，可浏览与加购，提交订单前请先登录。</p>
          <Link className="btn btn-primary" to="/login">
            去登录
          </Link>
        </SurfaceCard>
      ) : null}
      <SurfaceCard>
        <h3>地址</h3>
        {addresses.map((a) => (
          <label className="address-option" key={a.id}>
            <input type="radio" checked={selectedAddressId === a.id} onChange={() => setSelectedAddressId(a.id)} />
            {a.receiverName} {a.phone} {a.region} {a.detail}
          </label>
        ))}
      </SurfaceCard>
      <SurfaceCard>
        <label>
          优惠码
          <input value={promoCode} onChange={(e) => setPromoCode(e.target.value)} />
        </label>
        <label>
          发票抬头
          <select value={invoiceId} onChange={(e) => setInvoiceId(e.target.value)}>
            <option value="">不开票</option>
            {invoices.map((inv) => (
              <option key={inv.id} value={inv.id}>
                {inv.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          订单备注
          <input value={remark} onChange={(e) => setRemark(e.target.value)} />
        </label>
        <p className="muted">备注/发票字段已前端接入，待后端扩展持久化。</p>
        <button type="button" className="btn btn-primary" onClick={() => void place()} disabled={!selectedAddressId || !isAuthed}>
          提交订单
        </button>
        {orderId ? (
          <div className="row">
            <button type="button" onClick={() => navigate(`/orders/${orderId}`)}>
              查看订单
            </button>
            <button type="button" onClick={() => void api.simulatePay(orderId)}>
              立即支付
            </button>
          </div>
        ) : null}
        {error && <p className="error">{error}</p>}
      </SurfaceCard>
    </section>
  )
}
