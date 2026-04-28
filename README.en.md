# Navyblue · DaprFx

**Language:** English | [简体中文](./README.md)

## Overview

**DaprFx** is a lightweight, production-oriented microservice toolkit for **.NET** (sample services target **.NET 10**), built on **[Dapr](https://dapr.io/)**. It offers a **Spring Cloud–style** developer experience: typed abstractions, resilient invocation, pub/sub, outbox delivery, and state management—without binding you to a single vendor runtime.

This repository contains the **framework libraries** (`src/DaprFx.*`), **reference microservices** (`samples/*`), **Docker Compose** topology, **optional observability stack**, and **React frontends** for end-to-end demonstrations.

---

## Key Capabilities

| Area | Description |
|------|-------------|
| **Service invocation** | Interface-centric dynamic proxies with retries, timeouts, and circuit breaking |
| **Client topology** | Multi–`app-id` registration with **RoundRobin / Random / Sticky** load balancing |
| **Messaging** | Dapr pub/sub integration and reliable **outbox** publishing |
| **Resilience** | Idempotency stores, dead-letter handling, and configurable backoff |
| **State** | Generic Dapr state-store abstraction with typed repositories |
| **Configuration** | Dapr configuration provider with safe refresh semantics |
| **Security** | Cryptographic service abstraction; JWT-based service auth in samples |
| **Gateway** | BFF-style forwarding, rate limiting, and structured error envelopes |

---

## Repository Layout

| Path | Role |
|------|------|
| `src/DaprFx.Core` | Core abstractions, options, and primitives |
| `src/DaprFx.ServiceInvocation` | Dynamic invocation proxy and policies |
| `src/DaprFx.EventBus` | Event bus, outbox, idempotency, dead-letter |
| `src/DaprFx.StateManagement` | Typed state access helpers |
| `src/DaprFx.Configuration` | Configuration provider & refresh |
| `src/DaprFx.Cryptography` | Crypto abstraction implementation |
| `src/DaprFx.Hosting` | Hosting & DI registration extensions |
| `src/DaprFx.Operations` | Ops dashboard hooks (samples) |
| `samples/*Service` | Reference domain services (orders, catalog, auth, gateway, …) |
| `components/` | Dapr component specs (Redis state, pub/sub, …) |
| `deploy/` | Multi-node / infra-oriented compose fragments |
| `observability/` | Optional Prometheus, Grafana, Loki, Alertmanager, collector config |
| `docs/ops/` | SLO catalog, incident policy, runbooks, and operational templates |
| `scripts/` | Local lifecycle helpers (`dev-up.ps1`, `dev-down.ps1`, …) |

---

## Prerequisites

- **Docker Desktop** (or compatible engine) with Compose v2  
- **.NET SDK 10** (for local `dotnet` builds outside containers)  
- **Node.js 20+** (for frontend development)  
- Ports **5000–5013**, **6379**, **4317**, **16686** available on the host (defaults)

---

## Quick Start (Docker Compose)

### One-command stack (Windows PowerShell)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1
```

Skip image rebuild when images are already current:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1 -NoBuild
```

Shut down:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-down.ps1
```

The script pulls base .NET images, runs `docker compose up`, and performs **basic HTTP smoke checks** on critical endpoints.

### Plain Compose (any OS)

```bash
docker compose up --build -d
docker compose ps
```

Tear down:

```bash
docker compose down
```

---

## Runtime Profiles

| Profile | When to use | Entry |
|--------|----------------|-------|
| **Compose (default)** | Single-machine integration; Redis & Jaeger as services | `docker compose.yml` + `components/` |
| **Localhost components** | Host-run `dotnet` + local Redis | `components/local` — see `scripts/dev-local.ps1` |
| **Cluster-style hosts** | Multi-host names (Consul / Redis / OTLP by DNS) | `components/cluster`, `deploy/*`, `scripts/dev-cluster.ps1` |

Prepare per-node `.env` from `.env.example`. Typical variables: `CONSUL_HOST`, `REDIS_HOST`, `OTEL_HOST`, `INFRA_HOST_IP`.

---

## Service Catalog (default host ports)

| Service | URL | Notes |
|---------|-----|--------|
| Ops Portal | http://localhost:5000 | Aggregated service health / ops UI |
| OrderService | http://localhost:5001 | Orders, outbox, payments (sample) |
| ProductService | http://localhost:5002 | Legacy product API |
| ProductService (canary) | http://localhost:5003 | Canary variant |
| AuthService | http://localhost:5004 | Login, refresh, JWT issuance |
| UserService | http://localhost:5005 | Profiles, addresses, internal auth helpers |
| **GatewayService** | **http://localhost:5006** | **Primary BFF / public HTTP façade** |
| AuditService | http://localhost:5007 | Audit event sink |
| CatalogService | http://localhost:5008 | Catalog & merchandising |
| InventoryService | http://localhost:5009 | Stock & reservations |
| NotificationService | http://localhost:5010 | In-app / email style notifications |
| JobService | http://localhost:5011 | Operational jobs & reconciliations |
| PromotionService | http://localhost:5012 | Campaigns & validation |
| **Platform Admin** (static) | http://localhost:5013 | Platform operations SPA (nginx) |
| Jaeger UI | http://localhost:16686 | Trace visualization |
| OTLP (collector) | grpc://localhost:4317 | Application telemetry export |

> **Frontends (Vite dev servers):** `frontend/buyer-web`, `frontend/merchant-admin`, and `frontend/platform-admin` typically run on **5173+** when started with `npm run dev`. Point `VITE_GATEWAY_BASE_URL` to `http://localhost:5006`.

---

## Observability (optional stack)

A **separate** compose file defines Prometheus, Grafana, Loki, and Alertmanager for lab-style monitoring:

```bash
docker compose -f observability/docker-compose.observability.yml up -d
```

The main stack already ships **Jaeger** and an **OpenTelemetry Collector** (`observability/otel-collector-config.yaml`). Collector pipelines export traces to Jaeger and can expose Prometheus scrape endpoints—see `observability/prometheus/prometheus.yml` for scrape jobs used in lab deployments.

---

## Operations & Reliability

Governance and runbooks live under **`docs/ops/`**, including:

- SLO catalog and incident severity policy  
- Service ownership matrix  
- Runbooks (gateway 5xx, Dapr sidecar, dead-letter backlog, triage overview)  
- Game-day checklist and post-mortem template  

For contract-level regression hints, see **`CONTRACT_TEST_CHECKLIST.md`**.

---

## Gateway Error Model

When the gateway cannot complete a downstream call, it returns a **stable JSON envelope** (non-`ApiResponse` shape), for example:

```json
{
  "errorCode": "gateway_timeout",
  "message": "Gateway timed out while waiting for downstream service.",
  "detail": "…",
  "correlationId": "…"
}
```

Common `errorCode` values: `gateway_timeout` (504), `downstream_unavailable` / `downstream_circuit_open` (502/503), `gateway_forwarding_failed`, `rate_limited` (429), `client_blocked` (403).

**Best practice:** send `x-correlation-id` (or rely on generated `traceparent`) and correlate **Gateway → domain service → Dapr sidecar** logs and Jaeger spans.

---

## API Response Conventions (sample services)

Most sample HTTP APIs use:

**Success:** `{ "success": true, "data": <payload>, "error": null, "traceId"?: "…" }`  
**Failure:** `{ "success": false, "data": null, "error": { "code", "message", "details?" }, "traceId"?: "…" }`

Representative error `code` strings include `not_found`, `invalid_request`, `unauthorized`, `conflict`, `upstream_error`, `internal_error`, `insufficient_inventory`, `order_creation_failed`, etc.—prefer **`ApiErrorCodes.*`** constants in C# over raw literals.

**Pagination:** `page`, `pageSize`; response `data` usually contains `items`, `page`, `pageSize`, `total` (defaults `page=1`, `pageSize≤200`).

**Exception:** `POST /api/promotions/validate` returns a **bare** `PromotionValidationResult` JSON for low-level Dapr invoke compatibility.

---

## Minimal Verification Flow

```bash
# Seed demo users (UserService)
curl -X POST http://localhost:5005/api/users/seed
curl -X POST http://localhost:5005/api/users/seed-admin

# Login via Gateway
curl -s -X POST http://localhost:5006/api/gw/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"account\":\"demo\",\"password\":\"demo123\"}"
```

Use `data.accessToken` as `Bearer` for subsequent `http://localhost:5006/api/gw/...` calls.

---

## Extended Scenarios & Sprint Walkthroughs

End-to-end flows (checkout with addresses, SKU inventory, simulated payment, after-sales, refunds, merchant sub-order permissions, catalog governance, gateway security admin APIs, etc.) are documented **in depth** in the **Chinese** [`README.md`](./README.md) (sections *5-minute experience* onward). Treat that document as the canonical **integration cookbook** if you need exhaustive `curl` sequences.

---

## Frontends

| Path | Description |
|------|-------------|
| `frontend/buyer-web` | Consumer storefront (React + Vite + TypeScript) |
| `frontend/merchant-admin` | Merchant console (orders, catalog, promotions) |
| `frontend/platform-admin` | Platform administration SPA (nginx production image on `:5013`) |

```bash
cd frontend/buyer-web
npm install && npm run dev
```

```bash
VITE_GATEWAY_BASE_URL=http://localhost:5006
```

Additional references: `frontend/buyer-web/docs/*.md`, `docs/buyer-management-contract.md`, `docs/prd-closure-status.md`.

---

## Troubleshooting

```bash
docker compose ps -a
docker compose logs -f gatewayservice gatewayservice-dapr
docker compose logs -f userservice userservice-dapr
docker compose logs -f otel-collector jaeger redis
```

Intermittent base-image pulls:

```bash
docker pull mcr.microsoft.com/dotnet/aspnet:10.0
docker pull mcr.microsoft.com/dotnet/sdk:10.0
```

---

## License & Contributing

This repository is a **reference implementation** for learning and extension. Adapt governance, secrets, and SLIs before any production deployment.

For contribution guidelines, follow existing code style in `samples/` and `src/`, keep framework changes backward-compatible when possible, and extend **`docs/ops/`** when you add user-visible failure modes.
