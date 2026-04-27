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
  }

  return (
    <section className="card">
      <h3>店铺设置</h3>
      <div className="row">
        <label>店铺ID</label>
        <input value={shopId} onChange={(e) => setShopIdInput(e.target.value)} />
      </div>
      <div className="row">
        <label>店铺名称</label>
        <input value={shopName} onChange={(e) => setShopName(e.target.value)} />
      </div>
      <div className="row">
        <label>联系邮箱</label>
        <input value={contact} onChange={(e) => setContact(e.target.value)} />
      </div>
      <div className="row">
        <label>店铺公告</label>
        <textarea value={notice} onChange={(e) => setNotice(e.target.value)} />
      </div>
      <div className="row">
        <button type="button" onClick={save}>保存设置</button>
        {message && <span>{message}</span>}
      </div>
    </section>
  )
}
