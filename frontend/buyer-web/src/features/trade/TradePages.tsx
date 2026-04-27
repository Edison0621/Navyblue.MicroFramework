import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../lib/api'
import type { CartLine, CatalogItem, InvoiceTitle, UserAddress } from '../../types'
import { money } from '../../shared/utils/format'
import { useToast } from '../../shared/ui/ToastProvider'

export function CartPage() {
  const [lines, setLines] = useState<CartLine[]>([])
  const [products, setProducts] = useState<Record<string, CatalogItem>>({})
  const [invalid, setInvalid] = useState<string[]>([])
  const navigate = useNavigate()

  useEffect(() => {
    const run = async () => {
      const cart = await api.getCart()
      setLines(cart.lines)
      const dict: Record<string, CatalogItem> = {}
      await Promise.all(
        cart.lines.map(async (line) => {
          const item = await api.getCatalogItem(line.productId)
          dict[line.productId] = item
        }),
      )
      setProducts(dict)
      setInvalid(cart.lines.filter((x) => !dict[x.productId]?.isActive).map((x) => x.productId))
    }
    void run()
  }, [])

  const total = useMemo(
    () =>
      lines.reduce((sum, line) => {
        const item = products[line.productId]
        const skuPrice = item?.skus?.find((sku) => sku.skuId === line.skuId)?.price
        return sum + (skuPrice ?? item?.price ?? 0) * line.quantity
      }, 0),
    [lines, products],
  )

  const save = async (next: CartLine[]) => {
    setLines(next)
    await api.replaceCart(next.map((x) => ({ productId: x.productId, skuId: x.skuId ?? undefined, quantity: x.quantity })))
  }

  return (
    <section>
      <h2>购物车</h2>
      {lines.map((line, idx) => (
        <article className="card" key={`${line.productId}-${line.skuId ?? 'na'}`}>
          <h3>{products[line.productId]?.name ?? line.productId}</h3>
          {invalid.includes(line.productId) ? <p className="error">商品已失效</p> : null}
          <div className="row">
            <button type="button" onClick={() => void save(lines.map((x, i) => (i === idx ? { ...x, quantity: Math.max(1, x.quantity - 1) } : x)))}>
              -
            </button>
            <span>{line.quantity}</span>
            <button type="button" onClick={() => void save(lines.map((x, i) => (i === idx ? { ...x, quantity: x.quantity + 1 } : x)))}>
              +
            </button>
            <button type="button" onClick={() => void save(lines.filter((_, i) => i !== idx))}>
              删除
            </button>
          </div>
        </article>
      ))}
      <p>预估到手价: {money(total)}</p>
      <button type="button" disabled={!lines.length} onClick={() => navigate('/checkout')}>
        去结算
      </button>
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

  useEffect(() => {
    const run = async () => {
      const list = await api.listMyAddresses()
      setAddresses(list)
      if (list[0]) setSelectedAddressId(list[0].id)
      const inv = await api.listInvoices()
      setInvoices(inv)
      if (inv[0]) setInvoiceId(inv[0].id)
    }
    void run()
  }, [])

  const place = async () => {
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
      <h2>结算台</h2>
      <div className="card">
        <h3>地址</h3>
        {addresses.map((a) => (
          <label className="address-option" key={a.id}>
            <input type="radio" checked={selectedAddressId === a.id} onChange={() => setSelectedAddressId(a.id)} />
            {a.receiverName} {a.phone} {a.region} {a.detail}
          </label>
        ))}
      </div>
      <div className="card">
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
        <button type="button" onClick={() => void place()} disabled={!selectedAddressId}>
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
      </div>
    </section>
  )
}
