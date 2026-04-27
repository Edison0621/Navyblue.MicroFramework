export type AuditEntry = { at: string; action: string; detail: string }

const KEY = 'merchant_admin_audit_entries'

export function listAudit(): AuditEntry[] {
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) return []
    const parsed = JSON.parse(raw) as AuditEntry[]
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return []
  }
}

export function recordAudit(action: string, detail: string) {
  const next = [{ at: new Date().toISOString(), action, detail }, ...listAudit()].slice(0, 200)
  localStorage.setItem(KEY, JSON.stringify(next))
}
