import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import type { LoyaltyInfo } from '../../types'
import { PageHeader, SurfaceCard, StatusPill } from '../../shared/ui/Storefront'

const membershipLevels = [
  { name: '普通会员', minGrowth: 0, color: '#999', benefits: ['基础购物权限', '普通客服'] },
  { name: '铜牌会员', minGrowth: 1000, color: '#cd7f32', benefits: ['9.8折优惠', '专属客服', '生日礼包'] },
  { name: '银牌会员', minGrowth: 5000, color: '#c0c0c0', benefits: ['9.5折优惠', '优先发货', '免费退换'] },
  { name: '金牌会员', minGrowth: 10000, color: '#ffd700', benefits: ['9折优惠', '专属活动', '积分加倍'] },
  { name: '钻石会员', minGrowth: 20000, color: '#b9f2ff', benefits: ['8.5折优惠', '私人管家', '专属礼盒'] },
]

export function MembershipPage() {
  const [loyalty, setLoyalty] = useState<LoyaltyInfo>({ level: '普通会员', points: 0, growthValue: 0 })
  const [activeTab, setActiveTab] = useState<'overview' | 'points' | 'growth'>('overview')

  useEffect(() => {
    void api.listLoyalty().then(setLoyalty)
  }, [])

  const currentLevelIndex = membershipLevels.findIndex((l) => l.name === loyalty.level)
  const nextLevel = membershipLevels[currentLevelIndex + 1]
  const progressPercent = nextLevel
    ? Math.min(100, ((loyalty.growthValue - membershipLevels[currentLevelIndex].minGrowth) /
        (nextLevel.minGrowth - membershipLevels[currentLevelIndex].minGrowth)) * 100)
    : 100

  return (
    <section>
      <PageHeader title="会员中心" subtitle="权益、积分、成长体系总览" />

      {/* 会员等级卡片 */}
      <SurfaceCard className="membership-card">
        <div className="membership-header">
          <div className="level-badge" style={{ background: membershipLevels[currentLevelIndex]?.color }}>
            {loyalty.level}
          </div>
          <div className="membership-stats">
            <div className="stat-item">
              <span className="stat-value">{loyalty.points}</span>
              <span className="stat-label">积分</span>
            </div>
            <div className="stat-item">
              <span className="stat-value">{loyalty.growthValue}</span>
              <span className="stat-label">成长值</span>
            </div>
          </div>
        </div>

        {nextLevel && (
          <div className="level-progress">
            <div className="progress-info">
              <span>升级进度</span>
              <span>{loyalty.growthValue}/{nextLevel.minGrowth}</span>
            </div>
            <div className="progress-bar">
              <div className="progress-fill" style={{ width: `${progressPercent}%` }} />
            </div>
            <p className="muted">再获得 {nextLevel.minGrowth - loyalty.growthValue} 成长值即可升级为 {nextLevel.name}</p>
          </div>
        )}
      </SurfaceCard>

      {/* Tabs 导航 */}
      <SurfaceCard className="membership-tabs">
        <div className="tab-header">
          <button
            type="button"
            className={`tab-btn ${activeTab === 'overview' ? 'active' : ''}`}
            onClick={() => setActiveTab('overview')}
          >
            会员权益
          </button>
          <button
            type="button"
            className={`tab-btn ${activeTab === 'points' ? 'active' : ''}`}
            onClick={() => setActiveTab('points')}
          >
            积分明细
          </button>
          <button
            type="button"
            className={`tab-btn ${activeTab === 'growth' ? 'active' : ''}`}
            onClick={() => setActiveTab('growth')}
          >
            成长值
          </button>
        </div>

        <div className="tab-content">
          {activeTab === 'overview' && (
            <div className="benefits-content">
              <h3>当前等级权益</h3>
              <div className="benefits-list">
                {membershipLevels[currentLevelIndex]?.benefits.map((benefit, idx) => (
                  <div key={idx} className="benefit-item">
                    <span className="benefit-icon">✓</span>
                    <span>{benefit}</span>
                  </div>
                ))}
              </div>

              <h3 className="section-title">全部等级</h3>
              <div className="levels-grid">
                {membershipLevels.map((level, idx) => (
                  <div
                    key={level.name}
                    className={`level-card ${idx === currentLevelIndex ? 'current' : ''}`}
                  >
                    <div className="level-icon" style={{ background: level.color }}>
                      {level.name.replace('会员', '')}
                    </div>
                    <h4>{level.name}</h4>
                    <p className="muted">成长值 ≥ {level.minGrowth}</p>
                    <ul className="level-benefits">
                      {level.benefits.map((benefit, bIdx) => (
                        <li key={bIdx}>{benefit}</li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </div>
          )}

          {activeTab === 'points' && (
            <div className="points-content">
              <h3>积分明细</h3>
              <div className="points-summary">
                <div className="points-total">
                  <span className="points-value">{loyalty.points}</span>
                  <span className="points-label">可用积分</span>
                </div>
              </div>
              <div className="points-list">
                <div className="points-item">
                  <div className="points-item-header">
                    <span>购物奖励</span>
                    <span className="points-positive">+100</span>
                  </div>
                  <p className="muted">2024-01-15 14:30</p>
                </div>
                <div className="points-item">
                  <div className="points-item-header">
                    <span>积分兑换</span>
                    <span className="points-negative">-50</span>
                  </div>
                  <p className="muted">2024-01-14 10:20</p>
                </div>
                <div className="points-item">
                  <div className="points-item-header">
                    <span>签到奖励</span>
                    <span className="points-positive">+10</span>
                  </div>
                  <p className="muted">2024-01-13 09:00</p>
                </div>
              </div>
              <div className="points-actions">
                <button className="btn btn-primary">积分兑换</button>
                <button className="btn">签到打卡</button>
              </div>
            </div>
          )}

          {activeTab === 'growth' && (
            <div className="growth-content">
              <h3>成长值明细</h3>
              <div className="growth-rules">
                <h4>成长值获取规则</h4>
                <ul>
                  <li>消费 1 元 = 1 成长值</li>
                  <li>完善资料 = 100 成长值</li>
                  <li>每日签到 = 5 成长值</li>
                  <li>评价商品 = 10 成长值/次</li>
                </ul>
              </div>
              <div className="growth-list">
                <div className="growth-item">
                  <div className="growth-item-header">
                    <span>订单消费</span>
                    <span className="growth-positive">+299</span>
                  </div>
                  <p className="muted">2024-01-15 订单号: xxx123</p>
                </div>
                <div className="growth-item">
                  <div className="growth-item-header">
                    <span>完善资料</span>
                    <span className="growth-positive">+100</span>
                  </div>
                  <p className="muted">2024-01-10 10:30</p>
                </div>
              </div>
            </div>
          )}
        </div>
      </SurfaceCard>
    </section>
  )
}
