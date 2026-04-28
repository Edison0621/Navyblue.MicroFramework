import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import type { CouponItem } from '../../types'
import { money } from '../../shared/utils/format'
import { EmptyState, PageHeader, SurfaceCard } from '../../shared/ui/Storefront'

export function AssetsPage() {
  const [coupons, setCoupons] = useState<CouponItem[]>([])

  useEffect(() => {
    void api.listCoupons().then(setCoupons)
  }, [])

  return (
    <section>
      <PageHeader title="资产中心" subtitle="优惠券、红包、礼品卡等资产管理" />
      <SurfaceCard>
        <h3>我的优惠券</h3>
        {coupons.length === 0 ? <EmptyState title="暂无优惠券" description="多逛逛活动会场，优惠很快就来" /> : null}
        {coupons.map((coupon) => (
          <p key={coupon.id}>
            {coupon.title} - {money(coupon.amount)} - {coupon.status}
          </p>
        ))}
      </SurfaceCard>
      <SurfaceCard>
        <h3>红包/津贴/礼品卡/余额</h3>
        <p className="muted">当前版本为可扩展占位，已预留资产模块入口与状态展示区。</p>
      </SurfaceCard>
    </section>
  )
}
