import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import type { CouponItem } from '../../types'
import { money } from '../../shared/utils/format'

export function AssetsPage() {
  const [coupons, setCoupons] = useState<CouponItem[]>([])

  useEffect(() => {
    void api.listCoupons().then(setCoupons)
  }, [])

  return (
    <section>
      <h2>资产中心</h2>
      <div className="card">
        <h3>我的优惠券</h3>
        {coupons.length === 0 ? <p>暂无优惠券</p> : null}
        {coupons.map((coupon) => (
          <p key={coupon.id}>
            {coupon.title} - {money(coupon.amount)} - {coupon.status}
          </p>
        ))}
      </div>
      <div className="card">
        <h3>红包/津贴/礼品卡/余额</h3>
        <p className="muted">当前版本为可扩展占位，已预留资产模块入口与状态展示区。</p>
      </div>
    </section>
  )
}
