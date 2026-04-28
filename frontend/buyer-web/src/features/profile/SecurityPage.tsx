import { useState } from 'react'
import { api } from '../../lib/api'
import { useToast } from '../../shared/ui/ToastProvider'
import { PageHeader, SurfaceCard } from '../../shared/ui/Storefront'

export function SecurityPage() {
  const [reason, setReason] = useState('不再使用该账户')
  const [submitted, setSubmitted] = useState(false)
  const { notify } = useToast()

  return (
    <section>
      <PageHeader title="账户安全" subtitle="密码找回、绑定信息与注销申请入口" />
      <SurfaceCard>
        <p>找回密码/绑定手机/绑定邮箱：当前版本为流程入口占位。</p>
      </SurfaceCard>
      <SurfaceCard>
        <h3>注销申请</h3>
        <label>
          原因
          <input value={reason} onChange={(e) => setReason(e.target.value)} />
        </label>
        <button
          type="button"
          onClick={() =>
            void api
              .submitCloseRequest(reason)
              .then(() => {
                setSubmitted(true)
                notify('已提交注销申请', 'success')
              })
              .catch((err: unknown) => notify(err instanceof Error ? err.message : '提交失败', 'error'))
          }
        >
          提交注销申请
        </button>
        {submitted ? <p>申请已提交，等待平台审核。</p> : null}
      </SurfaceCard>
    </section>
  )
}
