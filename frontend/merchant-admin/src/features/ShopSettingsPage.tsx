import { useState } from 'react'
import { getShopId, setShopId } from '../shared/auth'
import { recordAudit } from '../shared/audit'

const SHOP_NAME_KEY = 'merchant_admin_shop_name'
const CONTACT_KEY = 'merchant_admin_shop_contact'
const NOTICE_KEY = 'merchant_admin_shop_notice'

export function ShopSettingsPage() {
  const [shopId, setShopIdInput] = useState(getShopId())
  const [shopName, setShopName] = useState(localStorage.getItem(SHOP_NAME_KEY) ?? '示例旗舰店')
  const [contact, setContact] = useState(localStorage.getItem(CONTACT_KEY) ?? 'merchant@example.com')
  const [notice, setNotice] = useState(localStorage.getItem(NOTICE_KEY) ?? '欢迎光临本店')
  const [message, setMessage] = useState('')

  const save = () => {
    setShopId(shopId)
    localStorage.setItem(SHOP_NAME_KEY, shopName)
    localStorage.setItem(CONTACT_KEY, contact)
    localStorage.setItem(NOTICE_KEY, notice)
    recordAudit('merchant.shop.settings.updated', `${shopId}:${shopName}`)
    setMessage('保存成功')
    
    // 3秒后清除消息
    setTimeout(() => setMessage(''), 3000)
  }

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">店铺设置</h1>
        <p className="page-description">管理店铺基本信息和联系方式</p>
      </div>

      {message && (
        <div className="message message-success">
          <span>✅</span>
          <span>{message}</span>
        </div>
      )}

      <div className="card">
        <div className="card-header">
          <h3 className="card-title">基本信息</h3>
        </div>
        <div className="card-body">
          <div className="form-group">
            <label className="form-label form-label-required">店铺ID</label>
            <input
              className="form-input"
              value={shopId}
              onChange={(e) => setShopIdInput(e.target.value)}
              placeholder="请输入店铺ID"
            />
          </div>

          <div className="form-group">
            <label className="form-label form-label-required">店铺名称</label>
            <input
              className="form-input"
              value={shopName}
              onChange={(e) => setShopName(e.target.value)}
              placeholder="请输入店铺名称"
            />
          </div>

          <div className="form-group">
            <label className="form-label form-label-required">联系邮箱</label>
            <input
              className="form-input"
              type="email"
              value={contact}
              onChange={(e) => setContact(e.target.value)}
              placeholder="请输入联系邮箱"
            />
          </div>

          <div className="form-group">
            <label className="form-label">店铺公告</label>
            <textarea
              className="form-textarea"
              value={notice}
              onChange={(e) => setNotice(e.target.value)}
              placeholder="请输入店铺公告"
            />
          </div>

          <div className="toolbar" style={{ marginTop: '24px' }}>
            <button className="btn btn-primary" onClick={save}>
              💾 保存设置
            </button>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-header">
          <h3 className="card-title">说明</h3>
        </div>
        <div className="card-body">
          <ul style={{ paddingLeft: '20px', lineHeight: '2' }}>
            <li>店铺ID用于标识您的店铺，请妥善保管</li>
            <li>店铺名称将显示在买家端</li>
            <li>联系邮箱用于接收重要通知</li>
            <li>店铺公告将展示在店铺首页</li>
          </ul>
        </div>
      </div>
    </div>
  )
}
