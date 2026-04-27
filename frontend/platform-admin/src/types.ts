export type Role = 'admin' | 'super-admin' | 'ops'

export interface SessionUser {
  id: string
  username: string
  roles: string[]
}

export interface ApiErrorPayload {
  code: string
  message: string
}

export interface ApiEnvelope<T> {
  success: boolean
  data: T | null
  error: ApiErrorPayload | null
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

export interface BuyerUser {
  id: string
  username: string
  email: string
  status: string
  level: string
  points: number
  growthValue: number
  tags: string[]
  blacklist: { isBlacklisted: boolean; reason?: string; expiresAt?: string }
  closeRequest?: { status: string; reason?: string; requestedAt?: string }
}

export interface CatalogCategoryNode {
  id: string
  name: string
  parentId?: string
  sortOrder: number
  isVisible: boolean
  isEnabled: boolean
  children: CatalogCategoryNode[]
}

export interface CatalogItem {
  id: string
  name: string
  shopId: string
  auditStatus?: string
  isOnShelf: boolean
  categoryId?: string
}
