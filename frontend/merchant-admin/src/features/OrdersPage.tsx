import { useState } from 'react'
import { api } from '../shared/api'
import { getShopId } from '../shared/auth'
import { recordAudit } from '../shared/audit'
import type { AfterSaleSummary, OrderSummary } from '../types'

export function OrdersPage() {
  const [orders, setOrders] = useState<OrderSummary[]>([])
  const [afterSales, setAfterSales] = useState<AfterSaleSummary[]>([])
  const [shipOrderId, setShipOrderId] = useState('')
  const [shipSubOrderId, setShipSubOrderId] = useState('')
  const [carrier, setCarrier] = useState('SF')
  const [trackingNo, setTrackingNo] = useState('')
  const [error, setError] = useState('')

  const shopId = getShopId()

  const load = async () => {
    try {
      setError('')
      const q = new URLSearchParams({ page: '1', pageSize: '20' })
      const [o, a] = await Promise.all([api.listOrdersByShop(shopId, q), api.listAfterSalesByShop(shopId, q)])
      setOrders(o.items)
      setAfterSales(a.items)
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    }
  }

  const ship = async () => {
    try {
      setError('')
      await api.shipSubOrder(shipOrderId, shipSubOrderId, carrier.trim() || undefined, trackingNo.trim() || undefined)
      recordAudit('merchant.order.shipped', `${shipOrderId}/${shipSubOrderId}`)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '发货失败')
    }
  }

  const deliver = async () => {
    try {
      setError('')
      await api.deliverSubOrder(shipOrderId, shipSubOrderId)
      recordAudit('merchant.order.delivered', `${shipOrderId}/${shipSubOrderId}`)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '确认送达失败')
    }
  }

  return (
    <section className="card">
      <h3>订单与售后</h3>
      <div className="row">
        <button type="button" onClick={() => void load()}>加载店铺订单</button>
        <span className="muted">shopId: {shopId}</span>
      </div>
      {error && <p className="error">{error}</p>}

      <div className="row">
        <input value={shipOrderId} onChange={(e) => setShipOrderId(e.target.value)} placeholder="orderId" />
        <input value={shipSubOrderId} onChange={(e) => setShipSubOrderId(e.target.value)} placeholder="subOrderId" />
        <input value={carrier} onChange={(e) => setCarrier(e.target.value)} placeholder="物流公司" />
        <input value={trackingNo} onChange={(e) => setTrackingNo(e.target.value)} placeholder="运单号" />
        <button type="button" onClick={() => void ship()}>发货</button>
        <button type="button" onClick={() => void deliver()}>确认送达</button>
      </div>

      <p>订单列表：</p>
      {orders.map((x) => (
        <p key={x.id}>{x.id} | {x.status} | {x.finalAmount}</p>
      ))}
      <p>售后列表：</p>
      {afterSales.map((x) => (
        <p key={`${x.orderId}-${x.afterSaleId}`}>{x.orderId}/{x.afterSaleId} | {x.status} | {x.requestedAmount ?? 0}</p>
      ))}
    </section>
  )
}
