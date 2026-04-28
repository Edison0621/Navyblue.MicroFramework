# Buyer-Web Redesign Baseline

## Route Coverage

- `/` -> new mall home page (JD-style operations entry)
- `/products` -> product list and filter/sort result page
- `/products/:id` -> product detail and add-to-cart entry
- `/cart` -> cart for guest and logged-in users
- `/checkout` -> checkout view, place-order only for logged-in users
- `/orders`, `/orders/:id` -> order list and detail (auth required)
- `/profile`, `/profile/invoices`, `/security` -> account center (auth required)
- `/membership`, `/assets`, `/activity` -> user center sub pages (auth required)
- `/after-sale`, `/reviews` -> unified placeholder pages

## Data Contracts

- Keep `src/lib/api.ts` as the single API adapter.
- Product flows use:
  - `listCatalogItems`, `getCatalogItem`
- Cart and trade flows use:
  - `getCart`, `replaceCart`, `checkout`, `simulatePay`
- Account and order flows use:
  - `listMyOrders`, `getOrder`, `cancelOrder`, `getTracking`
  - `getProfile`, `updateProfile`, `listMyAddresses`
  - `listInvoices`, `saveInvoice`, `listCoupons`, `listLoyalty`

## Guest Shopping Rule

- Guest user can:
  - browse mall pages
  - add products to cart
  - edit cart lines
- Guest user cannot:
  - place order
  - access account/order center pages

Implementation note:
- use local `guest cart` storage for unauthenticated shopping flow
- sync behavior can be introduced in a later iteration
