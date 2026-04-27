import { clearToken, getToken } from './auth'
import type { ApiEnvelope, BuyerUser, CatalogCategoryNode, CatalogItem, LoginResult, PagedResult, SessionUser } from '../types'

const base = import.meta.env.VITE_GATEWAY_BASE_URL ?? 'http://localhost:5006'

async function call<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken()
  const res = await fetch(`${base}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {}),
    },
  })
  const raw = await res.text()
  let payload: ApiEnvelope<T> | null = null
  if (raw) {
    try {
      payload = JSON.parse(raw) as ApiEnvelope<T>
    } catch {
      payload = null
    }
  }

  if (!payload) {
    if (res.status === 401) clearToken()
    throw new Error(raw || `request failed ${res.status}`)
  }

  if (!res.ok || !payload.success || payload.data === null) {
    if (res.status === 401) clearToken()
    throw new Error(payload.error?.message ?? `request failed ${res.status}`)
  }
  return payload.data
}

export const api = {
  login: (account: string, password: string) =>
    call<LoginResult>('/api/gw/auth/login', { method: 'POST', body: JSON.stringify({ account, password }) }),
  me: () => call<SessionUser>('/api/gw/auth/me'),
  listUsers: (query: URLSearchParams) => call<PagedResult<BuyerUser>>(`/api/gw/users?${query.toString()}`),
  patchTags: (id: string, tags: string[], note?: string) =>
    call<BuyerUser>(`/api/gw/users/${id}/tags`, { method: 'PATCH', body: JSON.stringify({ tags, note }) }),
  patchBlacklist: (id: string, isBlacklisted: boolean, reason?: string) =>
    call<BuyerUser>(`/api/gw/users/${id}/blacklist`, { method: 'PATCH', body: JSON.stringify({ isBlacklisted, reason }) }),
  patchLevel: (id: string, level: string, points?: number, growthValue?: number) =>
    call<BuyerUser>(`/api/gw/users/${id}/level`, { method: 'PATCH', body: JSON.stringify({ level, points, growthValue }) }),
  batchPatchTags: (userIds: string[], tags: string[], note?: string) =>
    call<BuyerUser[]>('/api/gw/users/batch/tags', { method: 'PATCH', body: JSON.stringify({ userIds, tags, note }) }),
  batchPatchBlacklist: (userIds: string[], isBlacklisted: boolean, reason?: string) =>
    call<BuyerUser[]>('/api/gw/users/batch/blacklist', { method: 'PATCH', body: JSON.stringify({ userIds, isBlacklisted, reason }) }),
  batchPatchLevel: (userIds: string[], level: string, points?: number, growthValue?: number, note?: string) =>
    call<BuyerUser[]>('/api/gw/users/batch/level', { method: 'PATCH', body: JSON.stringify({ userIds, level, points, growthValue, note }) }),
  reviewClose: (id: string, decision: 'approve' | 'reject', note?: string) =>
    call<BuyerUser>(`/api/gw/users/${id}/close-review`, { method: 'POST', body: JSON.stringify({ decision, note }) }),
  listCatalogItems: (query: URLSearchParams) => call<PagedResult<CatalogItem>>(`/api/gw/catalog/items?${query.toString()}`),
  approveItem: (id: string, note?: string) =>
    call(`/api/gw/catalog/items/${id}/approve`, { method: 'POST', body: JSON.stringify({ note }) }),
  rejectItem: (id: string, reason: string, note?: string) =>
    call(`/api/gw/catalog/items/${id}/reject`, { method: 'POST', body: JSON.stringify({ reason, note }) }),
  listCategories: () => call<CatalogCategoryNode[]>('/api/gw/catalog/categories'),
  listPromotions: (query: URLSearchParams) =>
    call<PagedResult<{ code: string; name: string; discountType: string; discountValue: number; isEnabled: boolean; startAt: string; endAt: string }>>(`/api/gw/promotions?${query.toString()}`),
  createPromotion: (input: { code: string; name: string; discountType: string; discountValue: number; startAt: string; endAt: string; isEnabled: boolean }) =>
    call('/api/gw/promotions', { method: 'POST', body: JSON.stringify(input) }),
  validatePromotion: (code: string, orderAmount: number) =>
    call('/api/gw/promotions/validate', { method: 'POST', body: JSON.stringify({ code, orderAmount }) }),
  listJobRuns: (query: URLSearchParams) =>
    call<PagedResult<{ id: string; jobName: string; status: string; message: string; createdAt: string }>>(`/api/gw/jobs/runs?${query.toString()}`),
  listOrdersByUser: (userId: string, query: URLSearchParams) =>
    call<PagedResult<{ id: string; status: string; finalAmount: number; createdAt: string }>>(`/api/gw/orders/by-user/${encodeURIComponent(userId)}/search?${query.toString()}`),
  listAfterSalesByShop: (shopId: string, query: URLSearchParams) =>
    call<PagedResult<{ orderId: string; afterSaleId: string; status: string; requestedAmount?: number }>>(`/api/gw/orders/by-shop/${encodeURIComponent(shopId)}/after-sales/search?${query.toString()}`),
  runJob: (name: 'reconcile-refunds' | 'expire-awaiting-payments' | 'reclaim-expired-inventory-reservations' | 'apply-catalog-shelf-schedules') =>
    call(`/api/gw/jobs/run/${name}`, { method: 'POST' }),
}
