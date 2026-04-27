# DaprFx

English | [中文](./README.md)

`DaprFx` is a lightweight microservice framework built on top of Dapr (sample projects currently target `.NET 10`).
It provides a Spring Cloud-like developer experience with:

- Interface-based service invocation
- Retry / timeout / circuit-breaker policies
- Multi-appId client registration with load balancing (`RoundRobin`, `Random`, `Sticky`)
- Dapr pub/sub event bus integration
- Reliable outbox delivery, idempotency, and dead-letter handling
- Generic state store abstraction
- Dynamic configuration refresh from Dapr configuration store
- Cryptography service abstraction

## Repository Layout

- `src/DaprFx.Core`: core abstractions and options
- `src/DaprFx.ServiceInvocation`: dynamic invocation proxy
- `src/DaprFx.EventBus`: event bus, outbox, dead-letter logic
- `src/DaprFx.StateManagement`: typed state storage
- `src/DaprFx.Configuration`: configuration provider and refresh loop
- `src/DaprFx.Cryptography`: crypto abstraction implementation
- `src/DaprFx.Hosting`: one-stop hosting + DI extensions
- `samples/OrderService`: consumer sample
- `samples/ProductService`: provider sample

## Quick Start (Recommended: Docker Compose)

### 1) One-click startup (Windows PowerShell)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1
```

Optional: skip build when images are already prepared:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1 -NoBuild
```

### 2) One-click shutdown

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-down.ps1
```

### 3) Runtime Modes (localhost / production)

#### A. localhost development mode (single machine)

Use this mode when developing on one machine and you want components such as Redis to be accessed via `localhost`.

- Component path: `components/local` (`redisHost=localhost:6379`)
- Start:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-local.ps1
```

- Stop:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-local-down.ps1
```

#### B. production / multi-host domain mode

Use this mode when deploying order/product services across multiple hosts with domain-based infra access.

- Component path: `components/cluster` and `deploy/*/components` (`redisHost=redis.infra.local:6379`)
- Prepare node-specific `.env` files from `.env.example`
- Start by node type:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-cluster.ps1 -Node infra
powershell -ExecutionPolicy Bypass -File .\scripts\dev-cluster.ps1 -Node order
powershell -ExecutionPolicy Bypass -File .\scripts\dev-cluster.ps1 -Node product
```

- Key environment variables:
  - `CONSUL_HOST` (e.g. `consul.infra.local`)
  - `REDIS_HOST` (e.g. `redis.infra.local`)
  - `OTEL_HOST` (e.g. `otel.infra.local`)
  - `INFRA_HOST_IP` (target IP for domain mapping)

### 4) Service URLs

- OrderService: [http://localhost:5001](http://localhost:5001)
- ProductService: [http://localhost:5002](http://localhost:5002)
- ProductService Canary: [http://localhost:5003](http://localhost:5003)
- Ops Portal: [http://localhost:5000](http://localhost:5000)
- Jaeger UI: [http://localhost:16686](http://localhost:16686)

## Manual Startup (Without Scripts)

```bash
docker compose up --build -d
docker compose ps
```

Stop:

```bash
docker compose down
```

## Local `dotnet run` Note

If you run services directly with `dotnet run`, ensure the Dapr runtime dependencies are available, otherwise you may see gRPC connection errors (for example, socket 10061):

- Dapr sidecar is running (`dapr run ...`)
- Redis/config components are reachable
- Component addresses match runtime environment (`redis:6379` in Docker network vs `localhost:6379` on host)

## 5-Minute Demo Flow

1. Start the full stack (script or compose).
2. Seed inventory and catalog (Sprint C adds SKU support; inventory key can be `productId::skuId`):

```bash
curl -X PUT http://localhost:5009/api/inventory/p-100::p-100-red-128 -H "Content-Type: application/json" -d "{\"quantity\":100}"
curl -X PUT http://localhost:5009/api/inventory/p-200 -H "Content-Type: application/json" -d "{\"quantity\":100}"
curl -X PUT http://localhost:5008/api/catalog/items/p-100 -H "Content-Type: application/json" -d "{\"name\":\"Demo A\",\"price\":50,\"isActive\":true,\"shopId\":\"shop-east\",\"skus\":[{\"skuId\":\"p-100-red-128\",\"name\":\"Red/128G\",\"price\":56,\"isActive\":true}]}"
curl -X PUT http://localhost:5008/api/catalog/items/p-200 -H "Content-Type: application/json" -d "{\"name\":\"Demo B\",\"price\":80,\"isActive\":true,\"shopId\":\"shop-west\"}"
```

3. **Login, shipping address, cart checkout (Sprint B + Sprint C SKU)** — cart and checkout bind to the JWT `NameIdentifier`; you cannot impersonate another user via path or body. Checkout requires **`addressId`**; OrderService loads the address from UserService over Dapr and snapshots it on the order (`shipTo*` fields). Line items now support optional `skuId`, and SKU price/inventory is used when provided.

```bash
curl -X POST http://localhost:5005/api/users/seed
curl -s -X POST http://localhost:5006/api/gw/auth/login -H "Content-Type: application/json" -d "{\"account\":\"demo\",\"password\":\"demo123\"}"

curl -X POST http://localhost:5006/api/gw/users/me/addresses -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"receiverName\":\"Zhang\",\"phone\":\"13800000000\",\"region\":\"Shanghai\",\"detail\":\"No.1 Demo Rd\",\"isDefault\":true}"
# Use data.id from the response as ADDRESS_ID

curl -X PUT http://localhost:5006/api/gw/carts/me -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"lines\":[{\"productId\":\"p-100\",\"skuId\":\"p-100-red-128\",\"quantity\":1},{\"productId\":\"p-200\",\"quantity\":2}]}"
curl -X POST http://localhost:5006/api/gw/orders/checkout -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"promoCode\":\"WELCOME10\",\"addressId\":\"ADDRESS_ID\"}"
```

The cart is cleared on success. The order starts in **`AwaitingPayment`** (inventory already reserved). `paymentDueAt` comes from `Order:PaymentTimeoutMinutes` (default **30**).

3b. **Simulated payment (Sprint A)** — substitute the order id from the response. Send the same user’s Bearer token; optional `idempotencyKey`.

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/pay -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"idempotencyKey\":\"demo-pay-1\"}"
```

Expire unpaid orders (releases inventory, sets `Cancelled`, publishes `order.cancelled`):

```bash
curl -X POST "http://localhost:5001/api/orders/ops/expire-awaiting-payments?maxAgeMinutes=30"
```

Or trigger via **JobService** (Docker sets `Jobs__OrderServiceBaseUrl` to OrderService; point your scheduler at this URL):

```bash
curl -X POST "http://localhost:5011/api/jobs/run/expire-awaiting-payments?maxAgeMinutes=30"
```

4. **Create order (SKU supported)** — JWT required; `userId` comes from the token (body `userId` is ignored).

```bash
curl -X POST http://localhost:5006/api/gw/orders -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"productId\":\"p-100\",\"skuId\":\"p-100-red-128\",\"quantity\":2,\"promoCode\":\"WELCOME10\"}"
```

4b. **Query enhancement (Sprint D)**:

- Order search: `/api/gw/orders/me/search` and `/api/gw/orders/by-user/{userId}/search` (admin), supports `status`, `productId`, `skuId`, `from`, `to`, `page`, `pageSize`.
- Catalog search: `/api/gw/catalog/items` supports `q`, `shopId`, `skuId`, `isActive`, `page`, `pageSize`.

```bash
curl "http://localhost:5006/api/gw/orders/me/search?status=AwaitingPayment&productId=p-100&skuId=p-100-red-128&page=1&pageSize=20" -H "Authorization: Bearer ACCESS_TOKEN"
curl "http://localhost:5006/api/gw/catalog/items?q=Demo&shopId=shop-east&skuId=p-100-red-128&page=1&pageSize=20" -H "Authorization: Bearer ACCESS_TOKEN"
```

4c. **After-sales workflow (Sprint E)**: order owner can submit after-sale requests; admin can approve/reject. Supports whole-order or sub-order scoped requests.

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/after-sales -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"subOrderId\":\"<subOrderId>\",\"reason\":\"damaged package\",\"detail\":\"box broken\",\"requestedAmount\":20}"
curl http://localhost:5006/api/gw/orders/<orderId>/after-sales -H "Authorization: Bearer ACCESS_TOKEN"

# admin review
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/after-sales/<afterSaleId>/approve -H "Authorization: Bearer ADMIN_ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"note\":\"approved\"}"
```

Sprint E events are wired to downstream consumers:

- `order.aftersale.requested`: persisted by Audit and notified by Notification service
- `order.aftersale.reviewed`: review result persisted by Audit and notified by Notification service

4d. **Payment/refund money-flow placeholder (Sprint F)**:

- Payment: `/api/orders/{orderId}/pay` executes capture through `IPaymentGateway` abstraction and stores `paymentTransactionId` (currently simulated gateway).
- Payment callback skeleton: `POST /api/orders/payments/callback` with HMAC verification via `x-payment-signature` + `x-payment-timestamp` and callback idempotency by `callbackId`.
- Callback failure branch: for `status=failed/cancelled`, reserved inventory is released, order is marked `Failed`, and `order.payment.failed` is published (plus `order.cancelled` for downstream compatibility).
- Refund: when after-sale is approved and `requestedAmount > 0`, refund is auto-triggered and stored on the after-sale record (`refundStatus/refundTransactionId/refundedAmount/refundedAt`).
- Refund event: `order.refunded` is published and consumed by Audit/Notification.

4e. **Refund idempotency + reconciliation job (Sprint G)**:

- Refund idempotency: refund result is persisted by `orderId + afterSaleId`; repeated approve/retry reuses the stored refund transaction and avoids duplicate gateway refund calls.
- Approve idempotency: approving an already-approved after-sale returns current state as successful idempotent response instead of conflict.
- Reconciliation API: `POST /api/orders/ops/reconcile-refunds?take=200` (admin) scans refund ledger entries against order after-sale refund state.
- Gateway/Job trigger: `POST /api/gw/jobs/run/reconcile-refunds?take=200` runs the reconciliation via JobService and stores a run record summary.

4f. **Expired inventory reservation reclaim (Sprint G.3)**:

- Reservation ledger: each `reserve` call now records a reservation entry (default TTL 30 minutes; optional `ttlMinutes` override).
- Normal release: `release` prefers matching by `reservationId`; without id, it releases earliest-expiring active reservations first.
- Reclaim API: `POST /api/inventory/ops/reclaim-expired-reservations?take=200` scans reservation ledgers and adds expired unreleased quantities back to stock.
- Gateway/Job trigger: `POST /api/gw/jobs/run/reclaim-expired-inventory-reservations?take=200` executes the reclaim workflow and persists a Job run summary.

4g. **Account status enforcement (PRD security gap)**:

- Critical order-domain write operations (cart update, order creation/checkout, pay, after-sale apply) now validate user status via internal UserService API.
- Only `status=active` can proceed; blocked/frozen users receive 403 with `user_disabled`.

4h. **Merchant sub-order scoped permission (PRD merchant gap)**:

- Sub-order operations (ship, deliver, sub-order cancel) now allow merchant-scoped access via JWT roles `shop:<shopId>` (or `shop-manager:<shopId>`).
- Admin can still operate all sub-orders; order owner can still operate own sub-orders; cross-shop sub-order operations are denied.
- Merchant order views: added `GET /api/orders/by-shop/{shopId}` and `GET /api/orders/by-shop/{shopId}/search` for shop-scoped sub-order queues (`orderStatus`, `subOrderStatus`, `productId`, `skuId`, time-range, pagination filters).
- Merchant after-sale workbench: added `GET /api/orders/by-shop/{shopId}/after-sales` and `GET /api/orders/by-shop/{shopId}/after-sales/search` with `status`, `refundStatus`, time-range, and pagination filters.

4i. **Catalog admin + shipment tracking closure (PRD full_plus + carrier_adapter)**:

- Category admin: added category tree APIs (create/update, sort, visibility, enable/disable), plus disable-to-unshelf linkage for items under that category.
- Category governance: added category delete API (blocked when child categories or linked items exist), and recursive subtree unshelf linkage when a category is disabled.
- Review flow: products support submit/approve/reject and audit history; shelf-on requires `AuditStatus=Approved`.
- Permission tightening: catalog write actions (upsert/submit/shelf/schedule) now require admin or shop-scoped roles (`shop:<shopId>` / `shop-manager:<shopId>`).
- Shelf rules: immediate on/off shelf and scheduled shelf windows (`onAt/offAt`) are supported, with idempotent scheduler endpoint `POST /api/gw/catalog/ops/apply-shelf-schedules`.
- Order rule alignment: OrderService now enforces “approved + currently on shelf + category enabled” in checkout validations, with backward compatibility fallback for legacy `isActive`-only catalog records.
- Tracking timeline: sub-orders now include `carrierCode/carrierName/trackingNumber` and timeline events (`CreatedAt/Status/Message/Source`). Shipping and delivery always append events. Query API: `GET /api/gw/orders/{orderId}/sub-orders/{subOrderId}/tracking`.
- Observability: new `order.shipped` and `order.delivered` events are published and consumed by Audit/Notification services.

4j. **Production-ready extension skeletons**:

- Payment gateway: `IPaymentGateway` result contracts now include `errorCode/retryable/gateway/gatewayTransactionId` to support real PSP integration and retry orchestration.
- Shipment provider: `ShipmentTracking` supports provider config (`provider/timeout/retry/deduplicate/source`) with `mock` as default implementation.
- Ops scheduling: catalog shelf scheduling is runnable from both ops endpoint and JobService route, with unified Job run records.

Fetch one order by id (owner or **admin** JWT):

```bash
curl http://localhost:5006/api/gw/orders/<orderId> -H "Authorization: Bearer ACCESS_TOKEN"
```

List **my** recent orders (`take` defaults to 20, max 100):

```bash
curl "http://localhost:5006/api/gw/orders/me?take=10" -H "Authorization: Bearer ACCESS_TOKEN"
```

List orders for a specific user id (**admin** gateway policy only):

```bash
curl "http://localhost:5006/api/gw/orders/by-user/<userId>?take=10" -H "Authorization: Bearer ADMIN_ACCESS_TOKEN"
```

The order payload includes main `status` (`Pending` / **`AwaitingPayment`** / `Confirmed` / `Completed` / `Cancelled` / `Failed`), `paymentDueAt` / `paidAt`, and `subOrders` with per-shop `fulfillmentStatus` (`PendingShipment` / `Shipped` / `Delivered` / `Cancelled`).

5. Check event processing output (includes local subscriptions such as `order.created`, **`order.paid`**, `order.cancelled`, and `order.completed`; Notification/Audit services also consume those topics):

```bash
curl http://localhost:5001/demo/events
```

6. Check configuration snapshot:

```bash
curl http://localhost:5001/demo/config
```

7. **Sub-order fulfillment (demo)**: the main order must be **`Confirmed` (simulated payment done)** before shipping. Each sub-order moves `PendingShipment` → `Shipped` → `Delivered`; when all sub-orders are delivered, the main order becomes `Completed`. Substitute `order.id` and each `order.subOrders[i].id`:

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/ship -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"trackingNumber\":\"SF123\",\"carrierCode\":\"SF\",\"carrierName\":\"ShunFeng\"}"
curl http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/tracking -H "Authorization: Bearer ACCESS_TOKEN"
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/deliver -H "Authorization: Bearer ACCESS_TOKEN"
```

(Repeat for every sub-order until the main order `Status` is `Completed`.)

8. **Cancellation (not shipped / unpaid)**: full cancel allows **`AwaitingPayment` or `Confirmed`** with **every** sub-order still `PendingShipment`, then inventory is released per line. Cancelling one sub-order releases only its lines; if every sub-order is cancelled, the main order becomes `Cancelled`. Shipped/delivered orders cannot be cancelled via these demo endpoints.

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/cancel -H "Authorization: Bearer ACCESS_TOKEN"
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/cancel -H "Authorization: Bearer ACCESS_TOKEN"
```

## Useful Troubleshooting Commands

```bash
docker compose ps -a
docker compose logs -f orderservice orderservice-dapr
docker compose logs -f productservice productservice-dapr
docker compose logs -f otel-collector jaeger
```

If image pulls fail intermittently (EOF/timeout), retry:

```bash
docker pull mcr.microsoft.com/dotnet/aspnet:10.0
docker pull mcr.microsoft.com/dotnet/sdk:10.0
```

## Dapr Components

Sample component files in `components/`:

- `pubsub.yaml` -> `orderpubsub`
- `statestore.yaml` -> `statestore`
- `appconfig.yaml` -> `appconfig`

The demo setup uses Redis by default.

## Key Options (Selected)

- `InvocationMaxRetries` / `InvocationTimeoutSeconds` / `CircuitBreaker*`
- `LoadBalancingStrategy` (`RoundRobin` / `Random` / `Sticky`)
- `OutboxMaxRetryCount` / `OutboxBaseDelaySeconds` / `OutboxDeadLetterStateKey`
- `IdempotencyTtlMinutes`
- `OtlpEndpoint` / `TelemetryServiceName` / `TelemetryEnvironment`

## Health and Endpoints

- Health checks: `/health/live`, `/health/ready`
- Dead-letter query (example): `GET /ops/outbox/deadletters?take=100`
- Dead-letter replay (example): `POST /ops/outbox/deadletters/replay-all?dryRun=true`
