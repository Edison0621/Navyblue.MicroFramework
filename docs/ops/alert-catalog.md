# Alert Catalog

## Critical Alerts

- `GatewayHigh5xxRate`
  - Condition: sustained 5xx from gateway.
  - Action: execute `runbooks/gateway-5xx-spike.md`.
- `OTelCollectorDown`
  - Condition: collector target down for 2+ minutes.
  - Action: validate collector container and exporter path.
- `DaprSidecarUnhealthy`
  - Condition: sidecar local health check fails repeatedly.
  - Action: execute `runbooks/dapr-sidecar-unhealthy.md`.

## Warning Alerts

- `AuthLatencyHighP95`
  - Condition: p95 exceeds SLO for 10 minutes.
  - Action: inspect downstream dependency latency.
- `DeadletterBacklogGrowing`
  - Condition: deadletter count increases continuously.
  - Action: execute `runbooks/deadletter-backlog.md`.

## Alert Metadata Standard

- Required labels:
  - `severity`
  - `service`
  - `owner`
  - `runbook`
