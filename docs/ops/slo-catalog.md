# SLO Catalog

## Scope

This document defines service-level objectives for production stability.

## Global Targets

- Availability SLO (monthly): 99.9% for core user paths.
- Error budget (monthly): 43m 49s for each core path.
- p95 latency targets:
  - auth login: <= 800ms
  - catalog query: <= 1200ms
  - checkout submit: <= 1500ms

## Core SLI Definitions

- Availability: `successful_requests / total_requests` for API endpoints.
- Latency: p95 and p99 per endpoint group.
- Dependency health:
  - Dapr sidecar healthy ratio
  - Redis state operation failure ratio
- Reliability amplification:
  - upstream retry-trigger rate
  - gateway 5xx and 504 rate

## Endpoint Groups

- Auth: `/api/gw/auth/*`
- Catalog: `/api/gw/catalog/*`
- Trade: `/api/gw/orders/*`, `/api/gw/carts/*`
- User center: `/api/gw/users/*`

## Breach Policy

- Warning: burn-rate > 2x for 15 minutes.
- Critical: burn-rate > 6x for 5 minutes.
- Emergency: 5xx > 10% for 5 minutes on any core endpoint group.
