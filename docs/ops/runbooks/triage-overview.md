# Triage Overview

## First 10 Minutes

1. Confirm blast radius:
   - impacted endpoints
   - affected user flows
2. Check dashboards:
   - gateway error rate
   - auth/catalog latency
   - collector and sidecar health
3. Classify severity from `incident-severity-policy.md`.

## Data Sources

- Metrics: Grafana / Prometheus
- Traces: Jaeger
- Logs: Loki + container logs
- Runtime summary: OpsPortal

## Immediate Mitigation

- Restart only unhealthy sidecars/services first.
- Enable temporary throttling on noisy upstreams.
- Apply fallback path if documented.

## Escalation

- Follow owner map in `service-ownership.md`.
