import type {
  ApiEnvelope,
  AfterSaleRequestInput,
  CatalogItem,
  CouponItem,
  FavoriteItem,
  FootprintItem,
  InvoiceTitle,
  LoginResult,
  LoyaltyInfo,
  Order,
  PagedResult,
  ReviewDraft,
  ShoppingCart,
  UserProfile,
  UserAddress,
} from '../types'
import { clearAccessToken, getAccessToken } from './session'

const apiBaseUrl = import.meta.env.VITE_GATEWAY_BASE_URL ?? 'http://localhost:5006'

class ApiError extends Error {
  public readonly code?: string
  public readonly status?: number

  constructor(
    message: string,
    code?: string,
    status?: number,
  ) {
    super(message)
    this.code = code
    this.status = status
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers ?? {})
  headers.set('Content-Type', 'application/json')

  const token = getAccessToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const res = await fetch(`${apiBaseUrl}${path}`, { ...init, headers })
  const raw = await res.text()
  let payload: ApiEnvelope<T> | null = null
  if (raw) {
    try {
      payload = JSON.parse(raw) as ApiEnvelope<T>
    } catch {
      // keep payload null; we will surface a readable error below
    }
  }

  if (!payload) {
    if (res.status === 401) {
      clearAccessToken()
    }
    const fallback = raw.trim() || `Request failed: ${res.status}`
    throw new ApiError(fallback, undefined, res.status)
  }

  if (!res.ok || !payload.success || payload.data === null) {
    if (res.status === 401) {
      clearAccessToken()
    }
    throw new ApiError(
      payload.error?.message ?? `Request failed: ${res.status}`,
      payload.error?.code,
      res.status,
    )
  }

  return payload.data
}

export const api = {
  login: (account: string, password: string) =>
    request<LoginResult>('/api/gw/auth/login', {
      method: 'POST',
      body: JSON.stringify({ account, password }),
    }),

  listCatalogItems: (query: URLSearchParams) =>
    request<PagedResult<CatalogItem>>(`/api/gw/catalog/items?${query.toString()}`),

  getCatalogItem: (id: string) => request<CatalogItem>(`/api/gw/catalog/items/${id}`),

  getCart: () => request<ShoppingCart>('/api/gw/carts/me'),

  replaceCart: (lines: Array<{ productId: string; skuId?: string; quantity: number }>) =>
    request<ShoppingCart>('/api/gw/carts/me', {
      method: 'PUT',
      body: JSON.stringify({ lines }),
    }),

  listMyAddresses: () => request<UserAddress[]>('/api/gw/users/me/addresses'),

  createAddress: (input: Omit<UserAddress, 'id'>) =>
    request<UserAddress>('/api/gw/users/me/addresses', {
      method: 'POST',
      body: JSON.stringify(input),
    }),

  checkout: (promoCode: string | null, addressId: string) =>
    request<Order>('/api/gw/orders/checkout', {
      method: 'POST',
      body: JSON.stringify({ promoCode: promoCode || null, addressId }),
    }),

  simulatePay: (orderId: string) =>
    request<Order>(`/api/gw/orders/${orderId}/pay`, {
      method: 'POST',
      body: JSON.stringify({ idempotencyKey: `buyer-web-${Date.now()}` }),
    }),

  listMyOrders: (query: URLSearchParams) =>
    request<PagedResult<Order>>(`/api/gw/orders/me/search?${query.toString()}`),

  getOrder: (id: string) => request<Order>(`/api/gw/orders/${id}`),

  cancelOrder: (id: string) =>
    request<Order>(`/api/gw/orders/${id}/cancel`, {
      method: 'POST',
    }),

  getTracking: (orderId: string, subOrderId: string) =>
    request(`/api/gw/orders/${orderId}/sub-orders/${subOrderId}/tracking`),

  getProfile: () => request<UserProfile>('/api/gw/users/me/profile'),

  updateProfile: (profile: UserProfile) =>
    request<UserProfile>('/api/gw/users/me/profile', {
      method: 'PUT',
      body: JSON.stringify(profile),
    }),

  listInvoices: () => request<InvoiceTitle[]>('/api/gw/users/me/invoices'),
  saveInvoice: (input: InvoiceTitle) =>
    request<InvoiceTitle>('/api/gw/users/me/invoices', {
      method: 'POST',
      body: JSON.stringify(input),
    }),

  listCoupons: (): Promise<CouponItem[]> => request('/api/gw/users/me/coupons'),
  listLoyalty: (): Promise<LoyaltyInfo> => request('/api/gw/users/me/loyalty'),
  listFavorites: (): Promise<FavoriteItem[]> => request('/api/gw/users/me/favorites'),
  saveFavorite: (item: FavoriteItem) =>
    request<FavoriteItem>('/api/gw/users/me/favorites', {
      method: 'POST',
      body: JSON.stringify(item),
    }),
  listFootprints: (): Promise<FootprintItem[]> => request('/api/gw/users/me/footprints'),
  saveFootprint: (item: FootprintItem) =>
    request<FootprintItem>('/api/gw/users/me/footprints', {
      method: 'POST',
      body: JSON.stringify(item),
    }),
  listSearches: (): Promise<string[]> => request('/api/gw/users/me/searches'),
  saveSearchTerm: (term: string): Promise<string[]> =>
    request('/api/gw/users/me/searches', {
      method: 'POST',
      body: JSON.stringify({ term }),
    }),
  listReviews: (): Promise<ReviewDraft[]> => request('/api/gw/users/me/reviews'),
  submitReview: (draft: ReviewDraft) =>
    request<ReviewDraft>('/api/gw/users/me/reviews', {
      method: 'POST',
      body: JSON.stringify(draft),
    }),
  createAfterSale: (orderId: string, input: AfterSaleRequestInput) =>
    request(`/api/gw/orders/${orderId}/after-sales`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  listAfterSales: (orderId: string) => request(`/api/gw/orders/${orderId}/after-sales`),
  submitCloseRequest: (reason: string) =>
    request('/api/gw/users/me/close-request', {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
}

export { ApiError }
