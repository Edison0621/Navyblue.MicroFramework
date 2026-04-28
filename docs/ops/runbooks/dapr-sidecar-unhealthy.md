# Runbook: Dapr Sidecar Unhealthy

## Symptoms

- Frequent `Connection refused` on Dapr state/service invocation.
- Upstream reports intermittent 500/502.

## Checks

1. Verify sidecar containers are running:
   - `docker compose ps`
2. Check sidecar logs:
   - `docker compose logs <service>-dapr`
3. Validate app-side local endpoint connectivity:
   - `http://localhost:<dapr-http-port>/v1.0/healthz`

## Mitigation

1. Recreate app and sidecar together:
   - `docker compose up -d --force-recreate <service> <service>-dapr`
2. Confirm no stale network namespace mismatch.
3. Verify dependency (Redis/state store) availability.

## Post-Action

- Monitor 15 minutes for repeated failures.
- If recurring, open Sev1 investigation with trace/log samples.
