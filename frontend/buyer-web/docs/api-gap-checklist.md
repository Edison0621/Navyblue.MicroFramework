# Buyer API Gap Checklist

This checklist tracks buyer-side PRD capability against available backend contracts.

## Ready via Gateway

- `POST /api/gw/auth/login`
- `GET /api/gw/catalog/items`
- `GET /api/gw/catalog/items/{id}`
- `GET/PUT /api/gw/carts/me`
- `GET/POST /api/gw/users/me/addresses`
- `POST /api/gw/orders/checkout`
- `POST /api/gw/orders/{orderId}/pay`
- `GET /api/gw/orders/me/search`
- `GET /api/gw/orders/{id}`
- `POST /api/gw/orders/{id}/cancel`
- `GET /api/gw/orders/{orderId}/sub-orders/{subOrderId}/tracking`
- `GET/POST /api/gw/orders/{orderId}/after-sales`

## Newly available via Gateway (closure wave)

- `GET/PUT /api/gw/users/me/profile`
- `GET /api/gw/users/me/loyalty`
- `GET/POST /api/gw/users/me/coupons`
- `GET/POST /api/gw/users/me/invoices`
- `GET/POST /api/gw/users/me/favorites`
- `GET/POST /api/gw/users/me/footprints`
- `GET/POST /api/gw/users/me/searches`
- `GET/POST /api/gw/users/me/reviews`

## Remaining backend gaps (production hardening)

- review media upload / append-review detail model is not finalized
- full after-sale state-machine + platform arbitration result query API set still needs explicit contract doc
- buyer assets beyond coupons (balance / gift card) are not yet exposed by dedicated gateway endpoints

## Frontend fallback policy

- local fallback is only allowed for truly missing server capabilities
- implemented routes must call gateway directly in `src/lib/api.ts`
- local mock data should not be used as system-of-record for core trade/governance paths
