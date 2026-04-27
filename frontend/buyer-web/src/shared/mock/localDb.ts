import type {
  CouponItem,
  FavoriteItem,
  FootprintItem,
  InvoiceTitle,
  LoyaltyInfo,
  ReviewDraft,
  UserProfile,
} from '../../types'

const PROFILE_KEY = 'buyer_web_profile'
const INVOICE_KEY = 'buyer_web_invoices'
const COUPON_KEY = 'buyer_web_coupons'
const FAVORITE_KEY = 'buyer_web_favorites'
const FOOTPRINT_KEY = 'buyer_web_footprints'
const SEARCH_KEY = 'buyer_web_searches'
const LOYALTY_KEY = 'buyer_web_loyalty'
const REVIEW_KEY = 'buyer_web_reviews'

function readJson<T>(key: string, fallback: T): T {
  const raw = localStorage.getItem(key)
  if (!raw) {
    return fallback
  }
  try {
    return JSON.parse(raw) as T
  } catch {
    return fallback
  }
}

function writeJson<T>(key: string, value: T): void {
  localStorage.setItem(key, JSON.stringify(value))
}

export const localDb = {
  getProfile(): UserProfile {
    return readJson<UserProfile>(PROFILE_KEY, {
      id: 'me',
      username: 'demo',
      email: 'demo@example.com',
      gender: 'unknown',
    })
  },
  saveProfile(profile: UserProfile): void {
    writeJson(PROFILE_KEY, profile)
  },
  listInvoices(): InvoiceTitle[] {
    return readJson<InvoiceTitle[]>(INVOICE_KEY, [])
  },
  saveInvoices(list: InvoiceTitle[]): void {
    writeJson(INVOICE_KEY, list)
  },
  listCoupons(): CouponItem[] {
    return readJson<CouponItem[]>(COUPON_KEY, [])
  },
  saveCoupons(list: CouponItem[]): void {
    writeJson(COUPON_KEY, list)
  },
  getLoyalty(): LoyaltyInfo {
    return readJson<LoyaltyInfo>(LOYALTY_KEY, { level: '普通会员', points: 0, growthValue: 0 })
  },
  saveLoyalty(info: LoyaltyInfo): void {
    writeJson(LOYALTY_KEY, info)
  },
  listFavorites(): FavoriteItem[] {
    return readJson<FavoriteItem[]>(FAVORITE_KEY, [])
  },
  saveFavorites(items: FavoriteItem[]): void {
    writeJson(FAVORITE_KEY, items)
  },
  listFootprints(): FootprintItem[] {
    return readJson<FootprintItem[]>(FOOTPRINT_KEY, [])
  },
  saveFootprints(items: FootprintItem[]): void {
    writeJson(FOOTPRINT_KEY, items)
  },
  listSearches(): string[] {
    return readJson<string[]>(SEARCH_KEY, [])
  },
  saveSearches(items: string[]): void {
    writeJson(SEARCH_KEY, items)
  },
  listReviews(): ReviewDraft[] {
    return readJson<ReviewDraft[]>(REVIEW_KEY, [])
  },
  saveReviews(items: ReviewDraft[]): void {
    writeJson(REVIEW_KEY, items)
  },
}
