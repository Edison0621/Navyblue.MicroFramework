export function recordAudit(action: string, detail: string) {
  const key = 'platform_admin_audit_logs'
  const raw = localStorage.getItem(key)
  const list = raw ? (JSON.parse(raw) as Array<{ action: string; detail: string; at: string }>) : []
  list.unshift({ action, detail, at: new Date().toISOString() })
  localStorage.setItem(key, JSON.stringify(list.slice(0, 200)))
}

export function listAudit() {
  const raw = localStorage.getItem('platform_admin_audit_logs')
  return raw ? (JSON.parse(raw) as Array<{ action: string; detail: string; at: string }>) : []
}
