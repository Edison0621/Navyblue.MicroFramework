import { clearToken, getToken } from './auth'
import type {
  AfterSaleSummary,
  ApiEnvelope,
  CatalogItem,
  LoginResult,
  OrderSummary,
  PagedResult,
  PromotionSummary,
  SessionUser,
} from '../types'

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
  listCatalogItems: (query: URLSearchParams) => call<PagedResult<CatalogItem>>(`/api/gw/catalog/items?${query.toString()}`),
  updateCatalogItem: (id: string, body: Record<string, unknown>) =>
    call(`/api/gw/catalog/items/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  submitCatalogItem: (id: string, note?: string) =>
    call(`/api/gw/catalog/items/${id}/submit`, { method: 'POST', body: JSON.stringify({ note }) }),
  getInventory: (productId: string) => call<{ productId: string; available: number; reserved: number }>(`/api/gw/inventory/${encodeURIComponent(productId)}`),
  listOrdersByShop: (shopId: string, query: URLSearchParams) =>
    call<PagedResult<OrderSummary>>(`/api/gw/orders/by-shop/${encodeURIComponent(shopId)}/search?${query.toString()}`),
  listAfterSalesByShop: (shopId: string, query: URLSearchParams) =>
    call<PagedResult<AfterSaleSummary>>(`/api/gw/orders/by-shop/${encodeURIComponent(shopId)}/after-sales/search?${query.toString()}`),
  shipSubOrder: (orderId: string, subOrderId: string, carrier?: string, trackingNo?: string) =>
    call(`/api/gw/orders/${encodeURIComponent(orderId)}/sub-orders/${encodeURIComponent(subOrderId)}/ship`, {
      method: 'POST',
      body: JSON.stringify({ shipping: carrier || trackingNo ? { carrier, trackingNo } : null }),
    }),
  deliverSubOrder: (orderId: string, subOrderId: string) =>
    call(`/api/gw/orders/${encodeURIComponent(orderId)}/sub-orders/${encodeURIComponent(subOrderId)}/deliver`, { method: 'POST' }),
  listPromotions: (query: URLSearchParams) => call<PagedResult<PromotionSummary>>(`/api/gw/promotions?${query.toString()}`),
  validatePromotion: (code: string, orderAmount: number) =>
    call('/api/gw/promotions/validate', { method: 'POST', body: JSON.stringify({ code, orderAmount }) }),
}
