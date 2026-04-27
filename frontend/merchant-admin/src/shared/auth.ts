const TOKEN_KEY = 'merchant_admin_access_token'
const SHOP_KEY = 'merchant_admin_shop_id'

export function getToken() {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string) {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY)
}

export function getShopId() {
  return localStorage.getItem(SHOP_KEY) ?? 'shop-default'
}

export function setShopId(shopId: string) {
  localStorage.setItem(SHOP_KEY, shopId.trim() || 'shop-default')
}
