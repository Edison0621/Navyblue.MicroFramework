export interface ApiErrorPayload {
  code: string
  message: string
  details?: unknown
}

export interface ApiEnvelope<T> {
  success: boolean
  data: T | null
  error: ApiErrorPayload | null
  traceId?: string
}

export interface LoginResult {
  accessToken: string
  refreshToken?: string
  tokenType: string
  expiresIn: number
}

export interface UserProfile {
  id: string
  username: string
  email: string
  avatarUrl?: string
  gender?: 'male' | 'female' | 'unknown'
  birthday?: string
}

export interface CatalogSku {
  skuId: string
  name: string
  price: number
  isActive: boolean
}

export interface CatalogItem {
  id: string
  name: string
  price: number
  isActive: boolean
  shopId: string
  categoryId?: string | null
  skus?: CatalogSku[]
}

export interface CartLine {
  productId: string
  skuId?: string | null
  quantity: number
}

export interface ShoppingCart {
  userId: string
  lines: CartLine[]
  updatedAt: string
}

export interface UserAddress {
  id: string
  receiverName: string
  phone: string
  region: string
  detail: string
  isDefault: boolean
}

export interface InvoiceTitle {
  id: string
  type: 'personal' | 'company'
  name: string
  taxNo?: string
  isDefault: boolean
}

export interface CouponItem {
  id: string
  title: string
  amount: number
  status: 'unused' | 'used' | 'expired'
  expireAt: string
}

export interface LoyaltyInfo {
  level: string
  points: number
  growthValue: number
}

export interface FavoriteItem {
  id: string
  type: 'product' | 'shop'
  targetId: string
  name: string
}

export interface FootprintItem {
  id: string
  productId: string
  name: string
  visitedAt: string
}

export interface OrderLine {
  productId: string
  skuId?: string | null
  skuName?: string | null
  quantity: number
  unitPrice: number
  lineTotal: number
  shopId: string
}

export interface SubOrder {
  id: string
  shopId: string
  lines: OrderLine[]
  subtotal: number
  fulfillmentStatus: string
  trackingNumber?: string | null
}

export interface Order {
  id: string
  userId?: string | null
  status: string
  originalAmount: number
  discountAmount: number
  finalAmount: number
  paymentDueAt?: string | null
  paidAt?: string | null
  createdAt: string
  updatedAt: string
  subOrders: SubOrder[]
}

export interface ReviewDraft {
  orderId: string
  subOrderId: string
  rating: number
  content: string
}

export interface AfterSaleRequestInput {
  subOrderId?: string
  reason: string
  detail?: string
  requestedAmount?: number
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}
