import { useAuth } from '../app/AuthContext'
import { getShopId } from '../shared/auth'

export function DashboardPage() {
  const { user } = useAuth()
  return (
    <section className="card">
      <h3>商家运营总览</h3>
      <p>当前账号：{user?.username ?? '-'}</p>
      <p>当前店铺：{getShopId()}</p>
      <p>已接入模块：商品管理、订单售后、营销活动、店铺设置与操作审计。</p>
    </section>
  )
}
