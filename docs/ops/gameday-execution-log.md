# GameDay Execution Log

## Drill 1: Dapr Sidecar Unavailable

- Date: 2026-04-28
- Scenario: stop one core sidecar and verify alert + recovery path.
- Expected:
  - alert triggers within 2 minutes
  - runbook restores service within 10 minutes
- Evidence to collect:
  - alert timestamp
  - mitigation command timeline
  - post-recovery metric screenshot link

## Drill 2: Redis Short Outage

- Date: 2026-04-28
- Scenario: temporary Redis interruption and recovery validation.
- Expected:
  - retries absorb transient outage
  - no cascading crash

## Drill 3: Downstream Timeout Storm

- Date: 2026-04-28
- Scenario: inject high downstream latency.
- Expected:
  - gateway timeout/circuit behavior visible
  - platform remains partially available
