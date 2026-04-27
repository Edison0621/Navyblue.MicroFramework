export interface ApiErrorPayload {
  code: string
  message: string
}

export interface ApiEnvelope<T> {
  success: boolean
  data: T | null
  error: ApiErrorPayload | null
  traceId?: string | null
}

export interface LoginResult {
  accessToken: string
  refreshToken?: string
  tokenType: string
  expiresIn: number
}

export interface SessionUser {
  userId: string
  username: string
  roles: string[]
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

export interface CatalogItem {
  id: string
  name: string
  shopId: string
  categoryId?: string
  auditStatus?: string
  isOnShelf: boolean
  stock?: number
  price?: number
}

export interface OrderSummary {
  id: string
  status: string
  finalAmount: number
  createdAt?: string
}

export interface AfterSaleSummary {
  orderId: string
  afterSaleId: string
  status: string
  requestedAmount?: number
}

export interface PromotionSummary {
  code: string
  name: string
  discountType: string
  discountValue: number
  isEnabled: boolean
  startAt: string
  endAt: string
}
