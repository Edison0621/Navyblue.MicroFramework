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
  const [message, setMessage] = useState('')
  const [activeTab, setActiveTab] = useState<'orders' | 'aftersales'>('orders')

  const shopId = getShopId()

  const load = async () => {
    try {
      setError('')
      setMessage('')
      const q = new URLSearchParams({ page: '1', pageSize: '20' })
      const [o, a] = await Promise.all([api.listOrdersByShop(shopId, q), api.listAfterSalesByShop(shopId, q)])
      setOrders(o.items)
      setAfterSales(a.items)
      setMessage('加载成功')
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    }
  }

  const ship = async () => {
    try {
      setError('')
      setMessage('')
      await api.shipSubOrder(shipOrderId, shipSubOrderId, carrier.trim() || undefined, trackingNo.trim() || undefined)
      recordAudit('merchant.order.shipped', `${shipOrderId}/${shipSubOrderId}`)
      setMessage('发货成功')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '发货失败')
    }
  }

  const deliver = async () => {
    try {
      setError('')
      setMessage('')
      await api.deliverSubOrder(shipOrderId, shipSubOrderId)
      recordAudit('merchant.order.delivered', `${shipOrderId}/${shipSubOrderId}`)
      setMessage('确认送达成功')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '确认送达失败')
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">订单与售后</h1>
        <p className="page-description">管理订单发货和售后处理</p>
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

      {/* Tabs 切换 */}
      <div className="card">
        <div className="card-header">
          <div className="toolbar">
            <button
              className={`btn ${activeTab === 'orders' ? 'btn-primary' : 'btn-ghost'}`}
              onClick={() => setActiveTab('orders')}
            >
              订单列表 ({orders.length})
            </button>
            <button
              className={`btn ${activeTab === 'aftersales' ? 'btn-primary' : 'btn-ghost'}`}
              onClick={() => setActiveTab('aftersales')}
            >
              售后列表 ({afterSales.length})
            </button>
          </div>
          <div className="card-extra">
            <button className="btn btn-sm" onClick={() => void load()}>
              🔄 刷新
            </button>
          </div>
        </div>

        <div className="card-body">
          {activeTab === 'orders' && (
            <div>
              {orders.length === 0 ? (
                <div className="empty-state">
                  <div className="empty-icon">🛒</div>
                  <p className="empty-text">暂无订单</p>
                  <button className="btn btn-primary" onClick={() => void load()}>
                    加载订单
                  </button>
                </div>
              ) : (
                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>订单ID</th>
                        <th>状态</th>
                        <th>金额</th>
                        <th>操作</th>
                      </tr>
                    </thead>
                    <tbody>
                      {orders.map((order) => (
                        <tr key={order.id}>
                          <td>
                            <span className="text-primary" style={{ fontWeight: 500 }}>
                              {order.id}
                            </span>
                          </td>
                          <td>
                            <span className={`badge ${
                              order.status === 'Completed' ? 'badge-success' :
                              order.status === 'Cancelled' ? 'badge-error' :
                              order.status === 'Pending' ? 'badge-warning' :
                              'badge-primary'
                            }`}>
                              {order.status}
                            </span>
                          </td>
                          <td>￥{order.finalAmount}</td>
                          <td>
                            <button className="btn btn-sm btn-primary">查看</button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {activeTab === 'aftersales' && (
            <div>
              {afterSales.length === 0 ? (
                <div className="empty-state">
                  <div className="empty-icon">🔄</div>
                  <p className="empty-text">暂无售后</p>
                </div>
              ) : (
                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>订单ID</th>
                        <th>售后ID</th>
                        <th>状态</th>
                        <th>金额</th>
                      </tr>
                    </thead>
                    <tbody>
                      {afterSales.map((item) => (
                        <tr key={`${item.orderId}-${item.afterSaleId}`}>
                          <td>{item.orderId}</td>
                          <td>{item.afterSaleId}</td>
                          <td>
                            <span className={`badge ${
                              item.status === 'Completed' ? 'badge-success' :
                              item.status === 'Rejected' ? 'badge-error' :
                              'badge-warning'
                            }`}>
                              {item.status}
                            </span>
                          </td>
                          <td>￥{item.requestedAmount ?? 0}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* 发货操作 */}
      <div className="card">
        <div className="card-header">
          <h3 className="card-title">发货操作</h3>
        </div>
        <div className="card-body">
          <div className="form-row">
            <div className="form-group">
              <label className="form-label form-label-required">订单ID</label>
              <input
                className="form-input"
                value={shipOrderId}
                onChange={(e) => setShipOrderId(e.target.value)}
                placeholder="请输入订单ID"
              />
            </div>

            <div className="form-group">
              <label className="form-label form-label-required">子订单ID</label>
              <input
                className="form-input"
                value={shipSubOrderId}
                onChange={(e) => setShipSubOrderId(e.target.value)}
                placeholder="请输入子订单ID"
              />
            </div>

            <div className="form-group">
              <label className="form-label">物流公司</label>
              <input
                className="form-input"
                value={carrier}
                onChange={(e) => setCarrier(e.target.value)}
                placeholder="请输入物流公司"
              />
            </div>

            <div className="form-group">
              <label className="form-label">运单号</label>
              <input
                className="form-input"
                value={trackingNo}
                onChange={(e) => setTrackingNo(e.target.value)}
                placeholder="请输入运单号"
              />
            </div>
          </div>

          <div className="toolbar" style={{ marginTop: '24px' }}>
            <button className="btn btn-primary" onClick={() => void ship()}>
              📦 发货
            </button>
            <button className="btn btn-success" onClick={() => void deliver()}>
              ✅ 确认送达
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
