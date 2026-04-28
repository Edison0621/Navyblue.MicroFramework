import type { CartLine } from '../types'

const GUEST_CART_KEY = 'buyer_web_guest_cart'

interface GuestCartPayload {
  lines: CartLine[]
  updatedAt: string
}

function readPayload(): GuestCartPayload {
  const raw = localStorage.getItem(GUEST_CART_KEY)
  if (!raw) {
    return { lines: [], updatedAt: new Date().toISOString() }
  }

  try {
    const parsed = JSON.parse(raw) as GuestCartPayload
    return { lines: parsed.lines ?? [], updatedAt: parsed.updatedAt ?? new Date().toISOString() }
  } catch {
    return { lines: [], updatedAt: new Date().toISOString() }
  }
}

export function getGuestCartLines(): CartLine[] {
  return readPayload().lines
}

export function setGuestCartLines(lines: CartLine[]): void {
  const payload: GuestCartPayload = {
    lines,
    updatedAt: new Date().toISOString(),
  }
  localStorage.setItem(GUEST_CART_KEY, JSON.stringify(payload))
}

