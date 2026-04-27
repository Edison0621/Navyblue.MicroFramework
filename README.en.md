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
2. Seed inventory and catalog (`shopId` drives per-shop sub-orders; without a catalog row, ProductService is used and the shop is `shop-default`):

```bash
curl -X PUT http://localhost:5009/api/inventory/p-100 -H "Content-Type: application/json" -d "{\"quantity\":100}"
curl -X PUT http://localhost:5009/api/inventory/p-200 -H "Content-Type: application/json" -d "{\"quantity\":100}"
curl -X PUT http://localhost:5008/api/catalog/items/p-100 -H "Content-Type: application/json" -d "{\"name\":\"Demo A\",\"price\":50,\"isActive\":true,\"shopId\":\"shop-east\"}"
curl -X PUT http://localhost:5008/api/catalog/items/p-200 -H "Content-Type: application/json" -d "{\"name\":\"Demo B\",\"price\":80,\"isActive\":true,\"shopId\":\"shop-west\"}"
```

3. **Cart checkout (main order + sub-orders per shop)** — replace the cart, then checkout; the cart state is cleared on success. The order starts in **`AwaitingPayment`** (inventory already reserved). `paymentDueAt` comes from `Order:PaymentTimeoutMinutes` (default **30**).

```bash
curl -X PUT http://localhost:5001/api/carts/demo-user -H "Content-Type: application/json" -d "{\"lines\":[{\"productId\":\"p-100\",\"quantity\":1},{\"productId\":\"p-200\",\"quantity\":2}]}"
curl -X POST http://localhost:5001/api/orders/checkout -H "Content-Type: application/json" -d "{\"userId\":\"demo-user\",\"promoCode\":\"WELCOME10\"}"
```

3b. **Simulated payment (Sprint A)** — substitute `data.order.id` from the response. Optional JSON `idempotencyKey` for safe retries.

```bash
curl -X POST http://localhost:5001/api/orders/<orderId>/pay -H "Content-Type: application/json" -d "{\"idempotencyKey\":\"demo-pay-1\"}"
```

Expire unpaid orders (releases inventory, sets `Cancelled`, publishes `order.cancelled`):

```bash
curl -X POST "http://localhost:5001/api/orders/ops/expire-awaiting-payments?maxAgeMinutes=30"
```

Or trigger via **JobService** (Docker sets `Jobs__OrderServiceBaseUrl` to OrderService; point your scheduler at this URL):

```bash
curl -X POST "http://localhost:5011/api/jobs/run/expire-awaiting-payments?maxAgeMinutes=30"
```

4. **Single-SKU order (legacy)** — optional `userId` so the order appears in the per-user list.

```bash
curl -X POST http://localhost:5001/api/orders -H "Content-Type: application/json" -d "{\"productId\":\"p-100\",\"quantity\":2,\"promoCode\":\"WELCOME10\",\"userId\":\"demo-user\"}"
```

Fetch one order by id:

```bash
curl http://localhost:5001/api/orders/<orderId>
```

List recent orders for a user (`take` defaults to 20, max 100):

```bash
curl "http://localhost:5001/api/orders/by-user/demo-user?take=10"
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
curl -X POST http://localhost:5001/api/orders/<orderId>/sub-orders/<subOrderId>/ship -H "Content-Type: application/json" -d "{\"trackingNumber\":\"SF123\"}"
curl -X POST http://localhost:5001/api/orders/<orderId>/sub-orders/<subOrderId>/deliver
```

(Repeat for every sub-order until the main order `Status` is `Completed`.)

8. **Cancellation (not shipped / unpaid)**: full cancel allows **`AwaitingPayment` or `Confirmed`** with **every** sub-order still `PendingShipment`, then inventory is released per line. Cancelling one sub-order releases only its lines; if every sub-order is cancelled, the main order becomes `Cancelled`. Shipped/delivered orders cannot be cancelled via these demo endpoints.

```bash
curl -X POST http://localhost:5001/api/orders/<orderId>/sub-orders/<subOrderId>/cancel
curl -X POST http://localhost:5001/api/orders/<orderId>/cancel
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
