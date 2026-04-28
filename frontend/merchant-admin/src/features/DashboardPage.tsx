import { useAuth } from '../app/AuthContext'
import { getShopId } from '../shared/auth'

const statsData = [
  { label: '今日订单', value: '128', icon: '🛒', color: 'primary', trend: '+12.5%' },
  { label: '今日销售额', value: '￥12,680', icon: '💰', color: 'success', trend: '+8.3%' },
  { label: '待处理订单', value: '23', icon: '⏳', color: 'warning', trend: '-5.2%' },
  { label: '商品总数', value: '156', icon: '📦', color: 'primary', trend: '+3.1%' },
]

const recentOrders = [
  { id: 'ORD-2024-001', product: 'iPhone 15 Pro', amount: '￥8,999', status: '待发货', time: '2024-01-15 14:30' },
  { id: 'ORD-2024-002', product: 'MacBook Air', amount: '￥7,999', status: '已发货', time: '2024-01-15 13:20' },
  { id: 'ORD-2024-003', product: 'AirPods Pro', amount: '￥1,899', status: '已完成', time: '2024-01-15 12:15' },
  { id: 'ORD-2024-004', product: 'iPad Mini', amount: '￥3,999', status: '待付款', time: '2024-01-15 11:00' },
]

export function DashboardPage() {
  const { user } = useAuth()
  
  return (
    <div>
      {/* 欢迎信息 */}
      <div className="page-header">
        <h1 className="page-title">欢迎回来，{user?.username || 'Admin'}!</h1>
        <p className="page-description">
          当前店铺：{getShopId()} | 这是您的运营数据总览
        </p>
      </div>

      {/* 统计卡片 */}
      <div className="grid grid-4" style={{ marginBottom: '24px' }}>
        {statsData.map((stat) => (
          <div key={stat.label} className="stat-card">
            <div className="stat-header">
              <div className={`stat-icon ${stat.color}`}>
                {stat.icon}
              </div>
            </div>
            <div>
              <div className="stat-value">{stat.value}</div>
              <div className="stat-label">{stat.label}</div>
            </div>
            <div className="stat-footer">
              较昨日 <span className={`text-${stat.trend.startsWith('+') ? 'success' : 'error'}`}>{stat.trend}</span>
            </div>
          </div>
        ))}
      </div>

      {/* 快捷操作 */}
      <div className="card" style={{ marginBottom: '24px' }}>
        <div className="card-header">
          <h3 className="card-title">快捷操作</h3>
        </div>
        <div className="card-body">
          <div className="toolbar">
            <button className="btn btn-primary">📦 发布商品</button>
            <button className="btn btn-success">🛒 处理订单</button>
            <button className="btn btn-warning">🎯 创建活动</button>
            <button className="btn">📊 查看报表</button>
          </div>
        </div>
      </div>

      {/* 最近订单 */}
      <div className="card">
        <div className="card-header">
          <h3 className="card-title">最近订单</h3>
          <div className="card-extra">
            <button className="btn btn-sm">查看全部</button>
          </div>
        </div>
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>订单号</th>
                <th>商品</th>
                <th>金额</th>
                <th>状态</th>
                <th>时间</th>
              </tr>
            </thead>
            <tbody>
              {recentOrders.map((order) => (
                <tr key={order.id}>
                  <td>{order.id}</td>
                  <td>{order.product}</td>
                  <td>{order.amount}</td>
                  <td>
                    <span className={`badge ${
                      order.status === '已完成' ? 'badge-success' :
                      order.status === '待发货' ? 'badge-warning' :
                      order.status === '已发货' ? 'badge-primary' :
                      'badge-default'
                    }`}>
                      {order.status}
                    </span>
                  </td>
                  <td>{order.time}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
