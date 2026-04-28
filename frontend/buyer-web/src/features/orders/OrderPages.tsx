import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../../lib/api'
import type { Order } from '../../types'
import { money } from '../../shared/utils/format'
import { useToast } from '../../shared/ui/ToastProvider'
import { EmptyState, PageHeader, StatusPill, SurfaceCard } from '../../shared/ui/Storefront'

export function OrdersPage() {
  const [status, setStatus] = useState('')
  const [orders, setOrders] = useState<Order[]>([])
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    try {
      const query = new URLSearchParams({ page: '1', pageSize: '20' })
      if (status) query.set('status', status)
      const res = await api.listMyOrders(query)
      setOrders(res.items)
    } catch (err) {
      setError(err instanceof Error ? err.message : '订单加载失败')
    }
  }, [status])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <section>
      <PageHeader title="我的订单" subtitle="统一查看全部订单状态和售后入口" />
      <div className="toolbar surface-card">
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">全部</option>
          <option value="AwaitingPayment">待付款</option>
          <option value="Confirmed">待发货</option>
          <option value="Completed">待评价</option>
          <option value="Cancelled">已取消</option>
        </select>
        <button type="button" onClick={() => void load()}>
          刷新
        </button>
      </div>
      {error && <p className="error">{error}</p>}
      {!error && orders.length === 0 ? <EmptyState title="暂无订单" description="先去首页逛逛，选点好货再回来" actionText="去首页" actionTo="/" /> : null}
      {orders.map((order) => (
        <SurfaceCard key={order.id}>
          <h3>{order.id}</h3>
          <p>
            状态: <StatusPill text={order.status} />
          </p>
          <p>实付: {money(order.finalAmount)}</p>
          <Link className="btn btn-primary" to={`/orders/${order.id}`}>
            详情
          </Link>
        </SurfaceCard>
      ))}
    </section>
  )
}

export function OrderDetailPage() {
  const { id = '' } = useParams()
  const [order, setOrder] = useState<Order | null>(null)
  const [review, setReview] = useState('')
  const [rating, setRating] = useState(5)
  const [afterSaleReason, setAfterSaleReason] = useState('商品破损')
  const [error, setError] = useState('')
  const { notify } = useToast()

  const load = useCallback(async () => {
    try {
      const detail = await api.getOrder(id)
      setOrder(detail)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    }
  }, [id])

  useEffect(() => {
    void load()
  }, [load])

  if (!order) return <p>{error || '加载中...'}</p>

  const firstSubOrder = order.subOrders[0]

  return (
    <section>
      <PageHeader title="订单详情" subtitle={`订单号 ${order.id}`} />
      <SurfaceCard>
        <p>订单号: {order.id}</p>
        <p>
          状态: <StatusPill text={order.status} />
        </p>
        <p>金额: {money(order.finalAmount)}</p>
        <div className="row">
          <button type="button" onClick={() => void api.simulatePay(order.id).then(load)}>
            模拟支付
          </button>
          <button type="button" onClick={() => void api.cancelOrder(order.id).then(load)}>
            取消订单
          </button>
          {firstSubOrder ? (
            <button
              type="button"
              onClick={() =>
                void api
                  .getTracking(order.id, firstSubOrder.id)
                  .then(() => notify('已查询物流', 'success'))
                  .catch((err: unknown) => setError(err instanceof Error ? err.message : '物流查询失败'))
              }
            >
              查看物流
            </button>
          ) : null}
        </div>
      </SurfaceCard>

      <SurfaceCard>
        <h3>评价</h3>
        <label>
          评分
          <input type="number" min={1} max={5} value={rating} onChange={(e) => setRating(Math.max(1, Math.min(5, Number(e.target.value) || 5)))} />
        </label>
        <label>
          内容
          <input value={review} onChange={(e) => setReview(e.target.value)} />
        </label>
        <button
          type="button"
          onClick={() =>
            void api.submitReview({
              orderId: order.id,
              subOrderId: firstSubOrder?.id ?? 'none',
              rating,
              content: review,
            }).then(() => notify('评价提交成功', 'success'))
          }
        >
          提交评价
        </button>
      </SurfaceCard>

      <SurfaceCard>
        <h3>售后申请</h3>
        <label>
          原因
          <input value={afterSaleReason} onChange={(e) => setAfterSaleReason(e.target.value)} />
        </label>
        <button
          type="button"
          onClick={() =>
            void api
              .createAfterSale(order.id, {
                subOrderId: firstSubOrder?.id,
                reason: afterSaleReason,
                requestedAmount: Math.max(order.finalAmount / 2, 1),
              })
              .then(() => notify('售后申请已提交', 'success'))
              .catch((err: unknown) => setError(err instanceof Error ? err.message : '申请失败'))
          }
        >
          申请售后
        </button>
      </SurfaceCard>
      {error && <p className="error">{error}</p>}
    </section>
  )
}
