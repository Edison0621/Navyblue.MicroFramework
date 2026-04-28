import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import type { CouponItem } from '../../types'
import { money } from '../../shared/utils/format'
import { EmptyState, PageHeader, SurfaceCard } from '../../shared/ui/Storefront'

interface AssetSummary {
  balance: number
  giftCard: number
  redPacket: number
  allowance: number
}

export function AssetsPage() {
  const [coupons, setCoupons] = useState<CouponItem[]>([])
  const [assetSummary, setAssetSummary] = useState<AssetSummary>({
    balance: 1280.50,
    giftCard: 500,
    redPacket: 50,
    allowance: 100,
  })
  const [activeTab, setActiveTab] = useState<'coupons' | 'balance' | 'giftcard' | 'redpacket'>('coupons')
  const [couponFilter, setCouponFilter] = useState<'all' | 'unused' | 'used' | 'expired'>('all')

  useEffect(() => {
    void api.listCoupons().then(setCoupons)
  }, [])

  const filteredCoupons = couponFilter === 'all'
    ? coupons
    : coupons.filter((c) => c.status === couponFilter)

  const unusedCount = coupons.filter((c) => c.status === 'unused').length

  return (
    <section>
      <PageHeader title="资产中心" subtitle="优惠券、红包、礼品卡等资产管理" />

      {/* 资产总览 */}
      <SurfaceCard className="assets-overview">
        <div className="asset-card balance-card">
          <div className="asset-icon">💰</div>
          <div className="asset-info">
            <span className="asset-label">余额</span>
            <span className="asset-value">{money(assetSummary.balance)}</span>
          </div>
          <button className="btn btn-small">充值</button>
        </div>
        <div className="asset-card giftcard-card">
          <div className="asset-icon">🎁</div>
          <div className="asset-info">
            <span className="asset-label">礼品卡</span>
            <span className="asset-value">{money(assetSummary.giftCard)}</span>
          </div>
          <button className="btn btn-small">绑定</button>
        </div>
        <div className="asset-card redpacket-card">
          <div className="asset-icon">🧧</div>
          <div className="asset-info">
            <span className="asset-label">红包</span>
            <span className="asset-value">{money(assetSummary.redPacket)}</span>
          </div>
          <button className="btn btn-small">查看</button>
        </div>
        <div className="asset-card allowance-card">
          <div className="asset-icon">🎫</div>
          <div className="asset-info">
            <span className="asset-label">津贴</span>
            <span className="asset-value">{money(assetSummary.allowance)}</span>
          </div>
          <button className="btn btn-small">查看</button>
        </div>
      </SurfaceCard>

      {/* Tabs 导航 */}
      <SurfaceCard className="assets-tabs">
        <div className="tab-header">
          <button
            type="button"
            className={`tab-btn ${activeTab === 'coupons' ? 'active' : ''}`}
            onClick={() => setActiveTab('coupons')}
          >
            优惠券 ({coupons.length})
          </button>
          <button
            type="button"
            className={`tab-btn ${activeTab === 'balance' ? 'active' : ''}`}
            onClick={() => setActiveTab('balance')}
          >
            余额
          </button>
          <button
            type="button"
            className={`tab-btn ${activeTab === 'giftcard' ? 'active' : ''}`}
            onClick={() => setActiveTab('giftcard')}
          >
            礼品卡
          </button>
          <button
            type="button"
            className={`tab-btn ${activeTab === 'redpacket' ? 'active' : ''}`}
            onClick={() => setActiveTab('redpacket')}
          >
            红包
          </button>
        </div>

        <div className="tab-content">
          {activeTab === 'coupons' && (
            <div className="coupons-content">
              <div className="coupon-filter">
                <button
                  type="button"
                  className={`filter-btn ${couponFilter === 'all' ? 'active' : ''}`}
                  onClick={() => setCouponFilter('all')}
                >
                  全部 ({coupons.length})
                </button>
                <button
                  type="button"
                  className={`filter-btn ${couponFilter === 'unused' ? 'active' : ''}`}
                  onClick={() => setCouponFilter('unused')}
                >
                  未使用 ({unusedCount})
                </button>
                <button
                  type="button"
                  className={`filter-btn ${couponFilter === 'used' ? 'active' : ''}`}
                  onClick={() => setCouponFilter('used')}
                >
                  已使用
                </button>
                <button
                  type="button"
                  className={`filter-btn ${couponFilter === 'expired' ? 'active' : ''}`}
                  onClick={() => setCouponFilter('expired')}
                >
                  已过期
                </button>
              </div>

              {filteredCoupons.length === 0 ? (
                <EmptyState title="暂无优惠券" description="多逛逛活动会场，优惠很快就来" />
              ) : (
                <div className="coupon-list">
                  {filteredCoupons.map((coupon) => (
                    <div key={coupon.id} className={`coupon-item ${coupon.status}`}>
                      <div className="coupon-left">
                        <div className="coupon-amount">
                          <span className="coupon-symbol">￥</span>
                          {coupon.amount.toFixed(2)}
                        </div>
                        <p className="coupon-condition">满199元可用</p>
                      </div>
                      <div className="coupon-right">
                        <h4>{coupon.title}</h4>
                        <p className="muted">有效期至 {new Date(coupon.expireAt).toLocaleDateString()}</p>
                        <span className={`coupon-status status-${coupon.status}`}>
                          {coupon.status === 'unused' && '未使用'}
                          {coupon.status === 'used' && '已使用'}
                          {coupon.status === 'expired' && '已过期'}
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {activeTab === 'balance' && (
            <div className="balance-content">
              <div className="balance-summary">
                <div className="balance-total">
                  <span className="balance-label">可用余额</span>
                  <span className="balance-value">{money(assetSummary.balance)}</span>
                </div>
              </div>
              <div className="balance-actions">
                <button className="btn btn-primary">充值</button>
                <button className="btn">提现</button>
              </div>
              <h3>余额明细</h3>
              <div className="balance-list">
                <div className="balance-item">
                  <div className="balance-item-header">
                    <span>充值</span>
                    <span className="balance-positive">+500.00</span>
                  </div>
                  <p className="muted">2024-01-15 14:30</p>
                </div>
                <div className="balance-item">
                  <div className="balance-item-header">
                    <span>订单支付</span>
                    <span className="balance-negative">-299.00</span>
                  </div>
                  <p className="muted">2024-01-14 10:20 订单号: xxx123</p>
                </div>
              </div>
            </div>
          )}

          {activeTab === 'giftcard' && (
            <div className="giftcard-content">
              <div className="giftcard-summary">
                <div className="giftcard-total">
                  <span className="giftcard-label">礼品卡总额</span>
                  <span className="giftcard-value">{money(assetSummary.giftCard)}</span>
                </div>
              </div>
              <div className="giftcard-actions">
                <button className="btn btn-primary">绑定礼品卡</button>
              </div>
              <h3>我的礼品卡</h3>
              <div className="giftcard-list">
                <div className="giftcard-item">
                  <div className="giftcard-header">
                    <h4>京东E卡</h4>
                    <span className="giftcard-balance">{money(500)}</span>
                  </div>
                  <p className="muted">卡号: **** **** **** 1234</p>
                  <p className="muted">有效期至 2025-12-31</p>
                </div>
              </div>
            </div>
          )}

          {activeTab === 'redpacket' && (
            <div className="redpacket-content">
              <div className="redpacket-summary">
                <div className="redpacket-total">
                  <span className="redpacket-label">红包总额</span>
                  <span className="redpacket-value">{money(assetSummary.redPacket)}</span>
                </div>
              </div>
              <h3>我的红包</h3>
              <div className="redpacket-list">
                <div className="redpacket-item">
                  <div className="redpacket-header">
                    <h4>新人红包</h4>
                    <span className="redpacket-amount">{money(20)}</span>
                  </div>
                  <p className="muted">满100元可用</p>
                  <p className="muted">有效期至 2024-02-15</p>
                  <span className="redpacket-status status-unused">未使用</span>
                </div>
                <div className="redpacket-item">
                  <div className="redpacket-header">
                    <h4>活动红包</h4>
                    <span className="redpacket-amount">{money(30)}</span>
                  </div>
                  <p className="muted">满200元可用</p>
                  <p className="muted">有效期至 2024-02-20</p>
                  <span className="redpacket-status status-unused">未使用</span>
                </div>
              </div>
            </div>
          )}
        </div>
      </SurfaceCard>
    </section>
  )
}
