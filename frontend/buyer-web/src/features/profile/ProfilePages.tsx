import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import type { InvoiceTitle, UserAddress, UserProfile } from '../../types'
import { useToast } from '../../shared/ui/ToastProvider'

export function ProfilePage() {
  const [profile, setProfile] = useState<UserProfile>({
    id: '',
    username: '',
    email: '',
    gender: 'unknown',
  })
  const [addresses, setAddresses] = useState<UserAddress[]>([])
  const [securityHint, setSecurityHint] = useState('建议定期修改密码并绑定邮箱')
  const { notify } = useToast()

  useEffect(() => {
    const run = async () => {
      setProfile(await api.getProfile())
      setAddresses(await api.listMyAddresses())
    }
    void run()
  }, [])

  return (
    <section>
      <h2>个人中心</h2>
      <div className="card">
        <label>
          昵称
          <input value={profile.username} onChange={(e) => setProfile((p) => ({ ...p, username: e.target.value }))} />
        </label>
        <label>
          邮箱
          <input value={profile.email} onChange={(e) => setProfile((p) => ({ ...p, email: e.target.value }))} />
        </label>
        <label>
          性别
          <select value={profile.gender ?? 'unknown'} onChange={(e) => setProfile((p) => ({ ...p, gender: e.target.value as UserProfile['gender'] }))}>
            <option value="unknown">未知</option>
            <option value="male">男</option>
            <option value="female">女</option>
          </select>
        </label>
        <label>
          生日
          <input type="date" value={profile.birthday ?? ''} onChange={(e) => setProfile((p) => ({ ...p, birthday: e.target.value }))} />
        </label>
        <button type="button" onClick={() => void api.updateProfile(profile).then(() => notify('资料已保存', 'success'))}>
          保存资料
        </button>
      </div>
      <div className="card">
        <h3>收货地址</h3>
        {addresses.map((a) => (
          <p key={a.id}>
            {a.receiverName} {a.phone} {a.region} {a.detail}
          </p>
        ))}
      </div>
      <div className="card">
        <h3>账户安全</h3>
        <input value={securityHint} onChange={(e) => setSecurityHint(e.target.value)} />
        <p className="muted">修改密码/绑定手机/注销账户：已预留流程入口。</p>
      </div>
    </section>
  )
}

export function InvoicePage() {
  const [type, setType] = useState<'personal' | 'company'>('personal')
  const [name, setName] = useState('')
  const [taxNo, setTaxNo] = useState('')
  const [list, setList] = useState<InvoiceTitle[]>([])
  const { notify } = useToast()

  useEffect(() => {
    void api.listInvoices().then(setList)
  }, [])

  const save = async () => {
    const saved = await api.saveInvoice({ id: '', type, name, taxNo, isDefault: list.length === 0 })
    setList((prev) => [saved, ...prev])
    setName('')
    setTaxNo('')
    notify('发票抬头已保存', 'success')
  }

  return (
    <section>
      <h2>发票抬头</h2>
      <div className="card">
        <label>
          类型
          <select value={type} onChange={(e) => setType(e.target.value as 'personal' | 'company')}>
            <option value="personal">个人</option>
            <option value="company">企业</option>
          </select>
        </label>
        <label>
          抬头名称
          <input value={name} onChange={(e) => setName(e.target.value)} />
        </label>
        {type === 'company' ? (
          <label>
            税号
            <input value={taxNo} onChange={(e) => setTaxNo(e.target.value)} />
          </label>
        ) : null}
        <button type="button" onClick={() => void save()} disabled={!name}>
          保存抬头
        </button>
      </div>
      {list.map((item) => (
        <div className="card" key={item.id}>
          {item.type === 'personal' ? '个人' : '企业'} - {item.name}
        </div>
      ))}
    </section>
  )
}
