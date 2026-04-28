# GameDay Checklist

## Scenario A: Sidecar Unavailable

- Stop one `*-dapr` container for a core service.
- Verify alert trigger time.
- Execute runbook and measure recovery time.

## Scenario B: Redis Short Outage

- Simulate Redis unavailability window.
- Validate retry/circuit behavior and user-facing degradation.
- Confirm recovery without manual data repair.

## Scenario C: Downstream Timeout Storm

- Inject latency to auth or catalog.
- Verify gateway timeout + circuit behavior.
- Confirm no cascading failure to unrelated paths.

## Success Criteria

- Detection under 2 minutes.
- Mitigation under 10 minutes for Sev1 scenarios.
- No unknown-error class remains unresolved.
