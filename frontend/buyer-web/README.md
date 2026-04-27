# Buyer Web Full Plan Baseline

Buyer-side frontend implementing the full-plan baseline:
- layered architecture (`app` / `features` / `shared`)
- full buyer-domain entry pages (account/profile/trade/order/review/after-sale/membership/assets/activity)
- quality gate (`lint` + `test` + `build`)

## Run

```bash
npm install
npm run dev
```

## Environment variables

Create `.env` if needed:

```bash
VITE_GATEWAY_BASE_URL=http://localhost:5006
```

## Implemented pages

- `/login` buyer login
- `/products` catalog list
- `/products/:id` product detail and add to cart
- `/cart` cart editing
- `/checkout` address selection and checkout
- `/orders` order list
- `/orders/:id` order detail, simulate pay, cancel, tracking
- `/profile` profile center
- `/profile/invoices` invoice titles
- `/membership` membership center
- `/assets` asset center
- `/activity` footprint/favorites/search history

## Quality gate

```bash
npm run lint
npm run test
npm run build
```

## Notes on API gaps

Some PRD features currently use local fallback storage until backend APIs are ready:
- membership points and growth details
- coupon/allowance/gift-card/balance detail APIs
- favorites, footprints, and rich review media APIs
- invoice persistence APIs

Gap details: `frontend/buyer-web/docs/api-gap-checklist.md`
