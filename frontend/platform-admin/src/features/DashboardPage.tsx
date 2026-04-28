export function DashboardPage() {
  const statsData = [
    { label: '商家总数', value: '1,256', icon: '🏪', color: 'primary', trend: '+5.2%' },
    { label: '商品总数', value: '45,890', icon: '📦', color: 'success', trend: '+12.3%' },
    { label: '待审核商品', value: '128', icon: '✅', color: 'warning', trend: '-8.1%' },
    { label: '今日订单', value: '3,456', icon: '🛒', color: 'primary', trend: '+15.7%' },
    { label: '用户总数', value: '125,678', icon: '👥', color: 'success', trend: '+6.8%' },
    { label: '客服工单', value: '42', icon: '🎫', color: 'warning', trend: '-3.2%' },
    { label: '本月营收', value: '￥1.2M', icon: '💰', color: 'primary', trend: '+22.4%' },
    { label: '风控预警', value: '8', icon: '🛡️', color: 'error', trend: '-12.5%' },
  ]

  const quickActions = [
    { label: '商家管理', path: '/merchant', icon: '🏪' },
    { label: '商品审核', path: '/product-audit', icon: '✅' },
    { label: '订单仲裁', path: '/order-governance', icon: '🛒' },
    { label: '用户治理', path: '/user-governance', icon: '👥' },
  ]

  const recentActivities = [
    { time: '2024-01-15 14:32', action: '商家A', detail: '状态更新为正常', type: 'success' },
    { time: '2024-01-15 14:28', action: '商品审核', detail: '通过商品 ID: 12345', type: 'success' },
    { time: '2024-01-15 14:25', action: '用户B', detail: '标签更新', type: 'primary' },
    { time: '2024-01-15 14:20', action: '营销活动', detail: '创建促销活动 PLAT10', type: 'warning' },
    { time: '2024-01-15 14:15', action: '风控配置', detail: '更新敏感词库', type: 'error' },
  ]

  return (
    <>
      {/* 统计卡片 */}
      <div className="grid grid-4">
        {statsData.map((stat) => (
          <div key={stat.label} className="stat-card">
            <div className={`stat-icon ${stat.color}`}>{stat.icon}</div>
            <div className="stat-value">{stat.value}</div>
            <div className="stat-label">{stat.label}</div>
            <div className="stat-footer">
              较昨日 <span className={`text-${stat.trend.startsWith('+') ? 'success' : 'error'}`}>{stat.trend}</span>
            </div>
          </div>
        ))}
      </div>

      {/* 快捷操作 */}
      <div className="card">
        <h3 className="card-title">快捷操作</h3>
        <div className="grid grid-4">
          {quickActions.map((action) => (
            <a key={action.path} href={action.path} className="btn btn-ghost btn-lg">
              <span>{action.icon}</span>
              <span>{action.label}</span>
            </a>
          ))}
        </div>
      </div>

      {/* 最近活动 */}
      <div className="card">
        <h3 className="card-title">最近操作记录</h3>
        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>时间</th>
                <th>操作类型</th>
                <th>详情</th>
              </tr>
            </thead>
            <tbody>
              {recentActivities.map((activity, index) => (
                <tr key={index}>
                  <td>{activity.time}</td>
                  <td>
                    <span className={`badge badge-${activity.type}`}>
                      {activity.action}
                    </span>
                  </td>
                  <td>{activity.detail}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </>
  )
}
