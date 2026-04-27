import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import type { LoyaltyInfo } from '../../types'

export function MembershipPage() {
  const [loyalty, setLoyalty] = useState<LoyaltyInfo>({ level: '普通会员', points: 0, growthValue: 0 })

  useEffect(() => {
    void api.listLoyalty().then(setLoyalty)
  }, [])

  return (
    <section>
      <h2>会员中心</h2>
      <div className="card">
        <p>当前等级: {loyalty.level}</p>
        <p>积分: {loyalty.points}</p>
        <p>成长值: {loyalty.growthValue}</p>
      </div>
      <div className="card">
        <h3>会员权益</h3>
        <ul>
          <li>会员专属价</li>
          <li>积分兑换优惠券</li>
          <li>活动提前购</li>
        </ul>
      </div>
    </section>
  )
}
