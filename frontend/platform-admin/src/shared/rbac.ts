import type { Role } from '../types'

export const permissionsByRole: Record<Role, string[]> = {
  admin: [
    'dashboard.view',
    'merchant.manage',
    'catalog.manage',
    'product.audit',
    'order.arbitrate',
    'user.manage',
    'marketing.manage',
    'content.manage',
    'finance.manage',
    'ticket.manage',
    'risk.manage',
    'system.manage',
  ],
  'super-admin': ['*'],
  ops: ['dashboard.view', 'order.arbitrate', 'user.manage', 'ticket.manage'],
}

export function hasPermission(userRoles: string[], permission: string): boolean {
  for (const role of userRoles) {
    const perms = permissionsByRole[role as Role]
    if (!perms) continue
    if (perms.includes('*') || perms.includes(permission)) return true
  }
  return false
}
