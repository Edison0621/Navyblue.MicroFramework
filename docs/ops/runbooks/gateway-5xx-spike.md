# Runbook: Gateway 5xx Spike

## Trigger

- `GatewayHigh5xxRate` alert firing for > 5 minutes.

## Investigation

1. Identify top failing routes in gateway logs.
2. Correlate failing downstream service from forwarding target.
3. Check downstream health/readiness and sidecar status.
4. Check if retries/circuit-open responses are rising.

## Mitigation

- If one downstream is unstable:
  - apply targeted restart for downstream + sidecar
  - keep gateway running and allow circuit breaker to protect callers
- If system-wide:
  - scale down noisy producers / apply temporary rate limits
  - prioritize auth, catalog, checkout path recovery

## Exit Criteria

- Gateway 5xx rate below 1% for 15 minutes.
