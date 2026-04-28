# Navyblue · DaprFx

**Language:** English | [简体中文](./README-zh.md)

## Project Overview

**DaprFx** is a lightweight microservice **toolkit and reference implementation** for **.NET** (samples are based on **.NET 10**) built on top of **[Dapr](https://dapr.io/)**.  
Its goal is to provide a **Spring Cloud-like** development experience without locking you into a single vendor runtime, covering typed abstractions, resilient invocation, eventing, Outbox delivery, state management, and observability integration.

This repository includes: framework libraries (`src/DaprFx.*`), domain sample services (`samples/*`), Docker Compose orchestration, optional observability stack, and React frontends for end-to-end demos and secondary development baseline.

---

## Core Capabilities

| Area | Description |
|------|-------------|
| **Service Invocation** | Interface-oriented dynamic proxy with retry/timeout/circuit-breaker |
| **Client Topology** | Multi-`appId` registration with **RoundRobin / Random / Sticky** load balancing |
| **Messaging** | Dapr Pub/Sub plus reliable **Outbox** delivery |
| **Resilience** | Idempotency store, dead-letter handling, configurable backoff |
| **State** | Generic Dapr state-store abstraction with repository examples |
| **Configuration** | Dapr configuration provider with safe refresh |
| **Security** | Encryption/decryption abstraction and JWT service identity in samples |
| **Gateway** | Backend-for-Frontend (BFF) forwarding, rate limiting, and structured error envelopes |

---

## Repository Structure

| Path | Responsibility |
|------|----------------|
| `src/DaprFx.Core` | Core abstractions, options, and primitives |
| `src/DaprFx.ServiceInvocation` | Dynamic invocation proxy and policies |
| `src/DaprFx.EventBus` | Event bus, Outbox, idempotency, dead-letter handling |
| `src/DaprFx.StateManagement` | Typed state access |
| `src/DaprFx.Configuration` | Configuration provider and refresh |
| `src/DaprFx.Cryptography` | Cryptography implementation |
| `src/DaprFx.Hosting` | One-stop hosting/DI extension |
| `src/DaprFx.Operations` | Ops dashboard extension points (sample) |
| `samples/*Service` | Reference domain microservices (order/catalog/gateway/auth, etc.) |
| `components/` | Dapr components (Redis state, Pub/Sub, etc.) |
| `deploy/` | Multi-node / infra-oriented compose fragments |
| `observability/` | Prometheus, Grafana, Loki, Alertmanager, collector configs |
| `docs/ops/` | SLO, incident policy, runbooks, game day, postmortem templates |
| `scripts/` | Local lifecycle scripts (`dev-up.ps1`, `dev-down.ps1`, etc.) |

---

## Prerequisites

- **Docker Desktop** (or compatible engine) with **Compose v2**
- **.NET SDK 10** (for host-side local build/run)
- **Node.js 20+** (frontend development)
- Host ports **5000–5013**, **6379**, **4317**, **16686** available by default

---

## Quick Start (Docker Compose)

### Windows (Recommended)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1
```

Skip rebuild if images are already ready:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1 -NoBuild
```

Stop:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-down.ps1
```

The script pre-pulls .NET base images, runs `docker compose up`, and performs basic endpoint smoke checks.

### Cross-platform

```bash
docker compose up --build -d
docker compose ps
docker compose down
```

---

## Runtime Modes

| Mode | Use case | Details |
|------|----------|---------|
| **Compose default** | Single-machine integration with Redis/Jaeger/Collector in containers | `docker-compose.yml` + `components/` |
| **localhost components** | Host `dotnet run` + local Redis | `components/local`, see `scripts/dev-local.ps1` |
| **Production-like multi-host** | Domain-based access to Consul/Redis/OTLP | `components/cluster`, `deploy/*`, see `scripts/dev-cluster.ps1` |

For cluster mode, prepare node-specific `.env` from `.env.example`. Common variables: `CONSUL_HOST`, `REDIS_HOST`, `OTEL_HOST`, `INFRA_HOST_IP`.

---

## Services and Ports (default host mapping)

| Service | URL | Notes |
|---------|-----|-------|
| Ops Portal | http://localhost:5000 | Aggregated operations entry |
| OrderService | http://localhost:5001 | Order/Outbox/payment sample |
| ProductService | http://localhost:5002 | Product sample provider |
| ProductService Canary | http://localhost:5003 | Canary instance |
| AuthService | http://localhost:5004 | Login, refresh, JWT |
| UserService | http://localhost:5005 | User, address, internal validation |
| **GatewayService** | **http://localhost:5006** | **External BFF / unified API gateway** |
| AuditService | http://localhost:5007 | Audit events |
| CatalogService | http://localhost:5008 | Catalog and governance |
| InventoryService | http://localhost:5009 | Stock and reservation |
| NotificationService | http://localhost:5010 | Notification |
| JobService | http://localhost:5011 | Operations/reconciliation jobs |
| PromotionService | http://localhost:5012 | Promotion campaigns |
| **Platform Admin** | http://localhost:5013 | Platform admin backend (static/Nginx) |
| Jaeger UI | http://localhost:16686 | Distributed tracing |
| OTLP Collector | grpc://localhost:4317 | Telemetry ingestion |

Frontends (Vite dev mode), such as `frontend/buyer-web` and `frontend/merchant-admin`, usually run on **5173+** with `npm run dev`. Point `VITE_GATEWAY_BASE_URL` to `http://localhost:5006`.

---

## Observability and Operations

- Main stack includes **Jaeger** and **OpenTelemetry Collector** (see `observability/otel-collector-config.yaml`).
- Optional enhanced stack (Prometheus/Grafana/Loki/Alertmanager):

```bash
docker compose -f observability/docker-compose.observability.yml up -d
```

- Operations documents: `docs/ops/` (SLO catalog, incident severity policy, service ownership, runbooks, game day checklist, postmortem template).
- Contract regression hint: `CONTRACT_TEST_CHECKLIST.md`.

---

## Unified Gateway Error Envelope (for integration)

When downstream calls fail, Gateway may return:

```json
{
  "errorCode": "gateway_timeout",
  "message": "Gateway timed out while waiting for downstream service.",
  "detail": "...",
  "correlationId": "..."
}
```

Common `errorCode` values:
- `gateway_timeout` (504)
- `downstream_unavailable` / `downstream_circuit_open` (502/503)
- `gateway_forwarding_failed`
- `rate_limited` (429)
- `client_blocked` (403)

Best practice: include `x-correlation-id` and correlate Gateway/service logs with Jaeger traces.

---

## Local Development (`dotnet run` only)

If you run services outside Compose, make sure:

- Dapr sidecar is running (`dapr run ...`) and matches configured `Dapr:GrpcEndpoint` / `Dapr:HttpEndpoint`.
- Redis and component addresses are consistent with your runtime environment (`redis:6379` in container network vs `localhost:6379` on host).

Otherwise you may see gRPC **connection refused** errors (e.g., Windows 10061).

---

## API Response Conventions (sample services)

Most sample APIs follow a unified envelope:

**Success:** `{ "success": true, "data": <payload>, "error": null, "traceId"?: "..." }`  
**Failure:** `{ "success": false, "data": null, "error": { "code", "message", "details?" }, "traceId"?: "..." }`

Common `error.code`: `not_found`, `invalid_request`, `unauthorized`, `conflict`, `upstream_error`, `internal_error`, `insufficient_inventory`, `order_creation_failed`, etc.  
Prefer service-specific `ApiErrorCodes.*` constants in C#.

Pagination:
- Query params: `page`, `pageSize`
- `data` usually contains `items`, `page`, `pageSize`, `total` (default `page=1`, max `pageSize=200`)

Exception:
- `POST /api/promotions/validate` returns a **raw** `PromotionValidationResult` JSON for direct Dapr invoke deserialization.

Gateway `GET` forwarding appends inbound query string to upstream URL for explicit `ForwardGet` routes.

---

## Minimal Verification

```bash
curl -X POST http://localhost:5005/api/users/seed
curl -X POST http://localhost:5005/api/users/seed-admin
curl -s -X POST http://localhost:5006/api/gw/auth/login -H "Content-Type: application/json" -d "{\"account\":\"demo\",\"password\":\"demo123\"}"
```

Use `data.accessToken` as `Authorization: Bearer ...` for subsequent `http://localhost:5006/api/gw/...` calls.

---

## Frontend Applications

| Path | Description |
|------|-------------|
| `frontend/buyer-web` | Buyer storefront (React + Vite + TypeScript) |
| `frontend/merchant-admin` | Merchant admin (orders, catalog, promotions) |
| `frontend/platform-admin` | Platform operations admin (`:5013` production image mapping) |

```bash
cd frontend/buyer-web
npm install && npm run dev
```

More references: `frontend/buyer-web/docs/`, `docs/buyer-management-contract.md`, `docs/prd-closure-status.md`.

---

## Appendix A: End-to-End Integration Commands (Detailed)

The following sections preserve detailed Sprint-based command playbooks, gateway security operations, and cross-domain validation flows.

## 5-Minute Experience Flow

1. Start the full stack (scripts or compose).
2. Prepare inventory and catalog (Sprint C SKU support; inventory key can be `productId::skuId`):

```bash
curl -X PUT http://localhost:5009/api/inventory/p-100::p-100-red-128 -H "Content-Type: application/json" -d "{\"quantity\":100}"
curl -X PUT http://localhost:5009/api/inventory/p-200 -H "Content-Type: application/json" -d "{\"quantity\":100}"
curl -X PUT http://localhost:5008/api/catalog/items/p-100 -H "Content-Type: application/json" -d "{\"name\":\"Demo A\",\"price\":50,\"isActive\":true,\"shopId\":\"shop-east\",\"skus\":[{\"skuId\":\"p-100-red-128\",\"name\":\"Red/128G\",\"price\":56,\"isActive\":true}]}"
curl -X PUT http://localhost:5008/api/catalog/items/p-200 -H "Content-Type: application/json" -d "{\"name\":\"Demo B\",\"price\":80,\"isActive\":true,\"shopId\":\"shop-west\"}"
```

3. **Login, address, cart checkout (Sprint B + Sprint C SKU):** cart and checkout bind to JWT `NameIdentifier`; impersonating another user via path/body is not allowed. Checkout requires `addressId`; OrderService fetches address via Dapr from UserService and snapshots `shipTo*` fields. Line items support optional `skuId`; if present, SKU price and SKU inventory are used.

```bash
curl -X POST http://localhost:5005/api/users/seed
curl -s -X POST http://localhost:5006/api/gw/auth/login -H "Content-Type: application/json" -d "{\"account\":\"demo\",\"password\":\"demo123\"}"
# Extract data.accessToken into ACCESS_TOKEN

curl -X POST http://localhost:5006/api/gw/users/me/addresses -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"receiverName\":\"Zhang San\",\"phone\":\"13800000000\",\"region\":\"Shanghai\",\"detail\":\"No.1 XX Rd\",\"isDefault\":true}"
# Get ADDRESS_ID from response data.id

curl -X PUT http://localhost:5006/api/gw/carts/me -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"lines\":[{\"productId\":\"p-100\",\"skuId\":\"p-100-red-128\",\"quantity\":1},{\"productId\":\"p-200\",\"quantity\":2}]}"
curl -X POST http://localhost:5006/api/gw/orders/checkout -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"promoCode\":\"WELCOME10\",\"addressId\":\"ADDRESS_ID\"}"
```

After success, the cart is cleared. Orders first enter **`AwaitingPayment`** (inventory reserved). `paymentDueAt` is controlled by `Order:PaymentTimeoutMinutes` (default 30).

3b. **Simulated payment (Sprint A):**

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/pay -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"idempotencyKey\":\"demo-pay-1\"}"
```

Expire overdue orders (release inventory, set order to `Cancelled`, publish `order.cancelled`):

```bash
curl -X POST "http://localhost:5001/api/orders/ops/expire-awaiting-payments?maxAgeMinutes=30"
```

Or trigger through JobService:

```bash
curl -X POST "http://localhost:5011/api/jobs/run/expire-awaiting-payments?maxAgeMinutes=30"
```

4. **Single order creation (SKU supported):** requires JWT; `userId` comes from token (body `userId` ignored).

```bash
curl -X POST http://localhost:5006/api/gw/orders -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"productId\":\"p-100\",\"skuId\":\"p-100-red-128\",\"quantity\":2,\"promoCode\":\"WELCOME10\"}"
```

4b. **Query enhancement (Sprint D):**
- Order search: `/api/gw/orders/me/search` and `/api/gw/orders/by-user/{userId}/search` (admin) with `status`, `productId`, `skuId`, `from`, `to`, `page`, `pageSize`
- Catalog search: `/api/gw/catalog/items` with `q`, `shopId`, `skuId`, `isActive`, `page`, `pageSize`

```bash
curl "http://localhost:5006/api/gw/orders/me/search?status=AwaitingPayment&productId=p-100&skuId=p-100-red-128&page=1&pageSize=20" -H "Authorization: Bearer ACCESS_TOKEN"
curl "http://localhost:5006/api/gw/catalog/items?q=Demo&shopId=shop-east&skuId=p-100-red-128&page=1&pageSize=20" -H "Authorization: Bearer ACCESS_TOKEN"
```

4c. **After-sales application (Sprint E):**

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/after-sales -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"subOrderId\":\"<subOrderId>\",\"reason\":\"damaged package\",\"detail\":\"box broken\",\"requestedAmount\":20}"
curl http://localhost:5006/api/gw/orders/<orderId>/after-sales -H "Authorization: Bearer ACCESS_TOKEN"

# admin review
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/after-sales/<afterSaleId>/approve -H "Authorization: Bearer ADMIN_ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"note\":\"approved\"}"
```

Sprint E events integrated downstream:
- `order.aftersale.requested`: audited + notification
- `order.aftersale.reviewed`: audited + notification

4d. **Payment/refund skeleton (Sprint F):**
- Payment through `IPaymentGateway`; writes `paymentTransactionId`
- Callback endpoint `POST /api/orders/payments/callback` supports HMAC verification (`x-payment-signature`, `x-payment-timestamp`) and callback idempotency (`callbackId`)
- Failed/cancelled callback releases reservation, marks order `Failed`, publishes `order.payment.failed` (and `order.cancelled` for compatibility)
- Approved after-sales with `requestedAmount > 0` auto-trigger refund and persist refund fields
- Publishes `order.refunded` for Audit/Notification subscribers

4e. **Refund idempotency + reconciliation job (Sprint G):**
- Refund idempotency by `orderId + afterSaleId`
- Approve idempotency for already-approved cases
- `POST /api/orders/ops/reconcile-refunds?take=200` scans consistency
- Trigger through `POST /api/gw/jobs/run/reconcile-refunds?take=200`

4f. **Expired reservation reclaim (Sprint G.3):**
- Reserve records ledger entries (default TTL 30 minutes, overridable with `ttlMinutes`)
- Release prioritizes explicit `reservationId`; otherwise earliest-expiry-first
- `POST /api/inventory/ops/reclaim-expired-reservations?take=200` reclaims expired unreleased quantities
- Trigger through `POST /api/gw/jobs/run/reclaim-expired-inventory-reservations?take=200`

4g. **Account status enforcement:**
- Order-domain writes (cart update/create order/pay/after-sales apply) validate user status via UserService internal APIs
- Only `status=active` can proceed; frozen/disabled users return 403 (`user_disabled`)

4h. **Merchant sub-order minimum permissions:**
- Sub-order operations (ship/deliver/cancel) allow roles `shop:<shopId>` or `shop-manager:<shopId>`
- Admin can operate all; buyers can operate own orders; no cross-shop access
- Added shop-scoped views:
  - `GET /api/orders/by-shop/{shopId}`
  - `GET /api/orders/by-shop/{shopId}/search`
  - `GET /api/orders/by-shop/{shopId}/after-sales`
  - `GET /api/orders/by-shop/{shopId}/after-sales/search`

4i. **Catalog admin + shipment tracking loop closure:**
- Category tree CRUD and enable/disable linkage
- Delete protection when subtree/items exist
- Product review workflow with audit history; on-shelf requires `AuditStatus=Approved`
- Catalog write auth tightened to admin or shop-scoped roles
- Immediate/scheduled shelf operations via `POST /api/gw/catalog/ops/apply-shelf-schedules`
- Order creation enforces reviewed/on-shelf/category-enabled constraints with legacy compatibility fallback
- Sub-orders track `carrierCode/carrierName/trackingNumber` and timeline events
- Tracking API: `GET /api/gw/orders/{orderId}/sub-orders/{subOrderId}/tracking`
- New events: `order.shipped`, `order.delivered`

4j. **Production extension boundaries:**
- Payment gateway result includes `errorCode/retryable/gateway/gatewayTransactionId`
- Shipment provider config supports provider/timeout/retry/deduplicate/source (`mock` default)
- Shelf schedule jobs can be triggered via ops and JobService with unified run records

Order response includes main status (`Pending` / `AwaitingPayment` / `Confirmed` / `Completed` / `Cancelled` / `Failed`), timestamps (`paymentDueAt`, `paidAt`), monetary fields, and `subOrders` (split by `shopId`) with sub-order `fulfillmentStatus` (`PendingShipment` / `Shipped` / `Delivered` / `Cancelled`).

Query one order (owner or admin):

```bash
curl http://localhost:5006/api/gw/orders/<orderId> -H "Authorization: Bearer ACCESS_TOKEN"
```

My orders:

```bash
curl "http://localhost:5006/api/gw/orders/me?take=10" -H "Authorization: Bearer ACCESS_TOKEN"
```

Orders by user (admin only):

```bash
curl "http://localhost:5006/api/gw/orders/by-user/<userId>?take=10" -H "Authorization: Bearer ADMIN_ACCESS_TOKEN"
```

5. Check event processing:

```bash
curl http://localhost:5001/demo/events
```

6. Check configuration snapshot:

```bash
curl http://localhost:5001/demo/config
```

7. **Sub-order fulfillment demo:** main order must be `Confirmed`; each sub-order transitions `PendingShipment -> Shipped -> Delivered`; all delivered means main order becomes `Completed`.

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/ship -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"trackingNumber\":\"SF123\",\"carrierCode\":\"SF\",\"carrierName\":\"ShunFeng\"}"
curl http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/tracking -H "Authorization: Bearer ACCESS_TOKEN"
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/deliver -H "Authorization: Bearer ACCESS_TOKEN"
```

8. **Cancellation (not shipped / unpaid):** full cancellation requires main order status in `AwaitingPayment` or `Confirmed`, and all sub-orders still `PendingShipment`; inventory is released by lines. Sub-order cancellation releases only that sub-order.

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/cancel -H "Authorization: Bearer ACCESS_TOKEN"
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/cancel -H "Authorization: Bearer ACCESS_TOKEN"
```

## Common Operations / Troubleshooting Commands

```bash
docker compose ps -a
docker compose logs -f orderservice orderservice-dapr
docker compose logs -f productservice productservice-dapr
docker compose logs -f otel-collector jaeger
```

Retry pulls when network is flaky:

```bash
docker pull mcr.microsoft.com/dotnet/aspnet:10.0
docker pull mcr.microsoft.com/dotnet/sdk:10.0
```

### Gateway Unified Error Return (integration recommendation)

```json
{
  "errorCode": "gateway_timeout",
  "message": "Gateway timed out while waiting for downstream service.",
  "detail": "...",
  "correlationId": "..."
}
```

Troubleshooting guidance:
- send `x-correlation-id` to Gateway for cross-service log correlation
- inspect `gatewayservice` and downstream container logs together
- use `traceparent` and Jaeger to identify slow/failing segments

## Dapr Components

`components/` includes:
- `pubsub.yaml` -> `orderpubsub`
- `statestore.yaml` -> `statestore`
- `appconfig.yaml` -> `appconfig`

Default sample setup depends on Redis.

## Core Config Items (excerpt)

- `InvocationMaxRetries` / `InvocationTimeoutSeconds` / `CircuitBreaker*`
- `LoadBalancingStrategy` (`RoundRobin` / `Random` / `Sticky`)
- `OutboxMaxRetryCount` / `OutboxBaseDelaySeconds` / `OutboxDeadLetterStateKey`
- `IdempotencyTtlMinutes`
- `OtlpEndpoint` / `TelemetryServiceName` / `TelemetryEnvironment`

## Gateway Anti-Abuse Configuration (Dev / Prod)

Gateway supports hot-updatable anti-abuse configuration.

Dev defaults:
- auth: `20 req / 60s`
- write: `30 req / 60s`
- read: `120 req / 60s`
- log level: `Information`

Prod recommendation:
- auth: `8 req / 60s`
- write: `20 req / 60s`
- read: `80 req / 60s`
- log level: `Warning`

Switch to production:

```bash
ASPNETCORE_ENVIRONMENT=Production
```

After go-live, observe `GET /api/gw/security/overview` and `GET /api/gw/security/rate-limit-metrics?take=20` for 1-2 days before tuning.

## Health Checks and Interfaces

- Health endpoints: `/health/live`, `/health/ready`
- Dead-letter query (example): `GET /ops/outbox/deadletters?page=1&pageSize=100`
- Dead-letter replay (example): `POST /ops/outbox/deadletters/replay-all?dryRun=true`

## New Service (M1 Skeleton) Quick Verification

1. Login for JWT:

```bash
curl -X POST http://localhost:5004/api/auth/login -H "Content-Type: application/json" -d "{\"account\":\"demo\",\"password\":\"demo123\"}"
```

2. Call protected APIs via Gateway:

```bash
curl http://localhost:5006/api/gw/orders/o-100 -H "Authorization: Bearer TOKEN"
```

Register through Gateway:

```bash
curl -X POST http://localhost:5006/api/gw/auth/register -H "Content-Type: application/json" -d "{\"username\":\"alice\",\"email\":\"alice@example.com\",\"password\":\"Passw0rd!\"}"
```

3. Seed/read users:

```bash
curl -X POST http://localhost:5005/api/users/seed
curl -X POST http://localhost:5005/api/users/seed-admin
curl "http://localhost:5005/api/users?page=1&pageSize=50"
```

4. Query audit:

```bash
curl "http://localhost:5007/api/audit/events?page=1&pageSize=20&action=order.created"
```

5. Quick verify additional services:

```bash
# Catalog
curl -X PUT http://localhost:5008/api/catalog/items/p-200 -H "Content-Type: application/json" -d "{\"name\":\"Keyboard\",\"price\":199.0,\"isActive\":true}"
curl "http://localhost:5008/api/catalog/items?page=1&pageSize=50"

# Inventory
curl -X PUT http://localhost:5009/api/inventory/p-200 -H "Content-Type: application/json" -d "{\"quantity\":50}"
curl -X POST http://localhost:5009/api/inventory/p-200/reserve -H "Content-Type: application/json" -d "{\"quantity\":2}"

# Notification
curl -X POST http://localhost:5010/api/notifications/send -H "Content-Type: application/json" -d "{\"channel\":\"email\",\"to\":\"demo@example.com\",\"title\":\"Hi\",\"body\":\"Welcome\"}"
curl "http://localhost:5010/api/notifications?page=1&pageSize=20"

# Job
curl -X POST http://localhost:5011/api/jobs/run/reconcile-inventory
curl "http://localhost:5011/api/jobs/runs?page=1&pageSize=20"

# Promotion
curl -X POST http://localhost:5012/api/promotions -H "Content-Type: application/json" -d "{\"code\":\"WELCOME10\",\"name\":\"Welcome Campaign\",\"discountType\":\"percentage\",\"discountValue\":10,\"startAt\":\"2026-01-01T00:00:00Z\",\"endAt\":\"2026-12-31T23:59:59Z\",\"isEnabled\":true}"
curl "http://localhost:5012/api/promotions?page=1&pageSize=20"
curl -X POST http://localhost:5012/api/promotions/validate -H "Content-Type: application/json" -d "{\"code\":\"WELCOME10\",\"orderAmount\":299}"
```

6. Gateway route verification:

```bash
# catalog
curl http://localhost:5006/api/gw/catalog/items -H "Authorization: Bearer TOKEN"
curl -X PUT http://localhost:5006/api/gw/catalog/items/p-300 -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d "{\"name\":\"Mouse\",\"price\":99.0,\"isActive\":true}"

# inventory
curl http://localhost:5006/api/gw/inventory/p-200 -H "Authorization: Bearer TOKEN"
curl -X POST http://localhost:5006/api/gw/inventory/p-200/reserve -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d "{\"quantity\":1}"

# jobs/notifications
curl -X POST http://localhost:5006/api/gw/jobs/run/reconcile-inventory -H "Authorization: Bearer TOKEN"
curl "http://localhost:5006/api/gw/notifications?page=1&pageSize=20" -H "Authorization: Bearer TOKEN"

# orders
curl -X POST http://localhost:5006/api/gw/orders -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d "{\"productId\":\"p-100\",\"quantity\":1,\"promoCode\":\"WELCOME10\"}"
curl http://localhost:5006/api/gw/orders/<orderId> -H "Authorization: Bearer TOKEN"

# promotions
curl -X POST http://localhost:5006/api/gw/promotions -H "Authorization: Bearer ADMIN_TOKEN" -H "Content-Type: application/json" -d "{\"code\":\"WELCOME10\",\"name\":\"Welcome Campaign\",\"discountType\":\"percentage\",\"discountValue\":10,\"startAt\":\"2026-01-01T00:00:00Z\",\"endAt\":\"2026-12-31T23:59:59Z\",\"isEnabled\":true}"
curl -X POST http://localhost:5006/api/gw/promotions/validate -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d "{\"code\":\"WELCOME10\",\"orderAmount\":299}"
```

7. Gateway rate limit and blacklist:

```bash
curl http://localhost:5006/api/gw/catalog/items -H "Authorization: Bearer TOKEN" -H "x-client-id: demo-client"
curl http://localhost:5006/api/gw/security/blacklist -H "Authorization: Bearer ADMIN_TOKEN"
curl -X POST http://localhost:5006/api/gw/security/blacklist -H "Authorization: Bearer ADMIN_TOKEN" -H "Content-Type: application/json" -d "{\"client\":\"client:demo-client\",\"ttlMinutes\":60}"
curl -X DELETE http://localhost:5006/api/gw/security/blacklist/client:demo-client -H "Authorization: Bearer ADMIN_TOKEN"
curl "http://localhost:5006/api/gw/security/rate-limit-metrics?take=20" -H "Authorization: Bearer ADMIN_TOKEN"
curl "http://localhost:5006/api/gw/security/rate-limit-metrics/client:demo-client" -H "Authorization: Bearer ADMIN_TOKEN"
curl -X DELETE "http://localhost:5006/api/gw/security/rate-limit-metrics/client:demo-client" -H "Authorization: Bearer ADMIN_TOKEN"
curl -X POST "http://localhost:5006/api/gw/security/rate-limit-metrics/reset" -H "Authorization: Bearer ADMIN_TOKEN"
curl -X DELETE "http://localhost:5006/api/gw/security/blacklist" -H "Authorization: Bearer ADMIN_TOKEN"
curl "http://localhost:5006/api/gw/security/overview" -H "Authorization: Bearer ADMIN_TOKEN"
curl "http://localhost:5006/api/gw/security/config" -H "Authorization: Bearer ADMIN_TOKEN"
curl -X PUT "http://localhost:5006/api/gw/security/config/rate-limit-policies/auth" -H "Authorization: Bearer ADMIN_TOKEN" -H "Content-Type: application/json" -d "{\"permitLimit\":10,\"windowSeconds\":60,\"queueLimit\":0}"
curl -X PUT "http://localhost:5006/api/gw/security/config/exempt-paths" -H "Authorization: Bearer ADMIN_TOKEN" -H "Content-Type: application/json" -d "{\"paths\":[\"/\",\"/health/live\",\"/health/ready\",\"/api/gw/security/overview\"]}"
```

Notes:
- Blacklist persists in `statestore` (survives Gateway restart)
- Blacklist supports `ttlMinutes` auto-unblock
- Exempt paths configurable via `Security.RateLimitExemptPaths`
- Tiered rate policies via `Security.RateLimitPolicies` (`auth` / `write` / `read`)

> Note: API envelope, error codes, and pagination conventions are already covered in the main section above.

## Contract Test Checklist

- Minimal contract test checklist: `CONTRACT_TEST_CHECKLIST.md`

## Frontend Supplementary Documents

- `frontend/buyer-web/docs/api-gap-checklist.md`
- `frontend/buyer-web/docs/release-checklist.md`
- `docs/buyer-management-contract.md`
- `docs/prd-closure-status.md`

---

## Usage Disclaimer

This repository is intended for **learning, demonstration, and secondary development baseline**.  
For production usage, you must complete your own hardening for secrets/config management, capacity and resilience planning, security review, SLO/alerting, and on-call process.  
Default demo secrets and relaxed rate-limit settings are **not** production-safe.
