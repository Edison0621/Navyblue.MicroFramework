# Incident Severity Policy

## Severity Levels

- Sev0
  - Full platform outage or data loss risk.
  - No viable workaround.
  - Response immediately.
- Sev1
  - Core business path unavailable for a major user segment.
  - Example: login or catalog query unavailable > 5 minutes.
  - Response within 10 minutes.
- Sev2
  - Partial degradation with workaround.
  - Error rate elevated but below Sev1 threshold.
  - Response within 30 minutes.
- Sev3
  - Minor impact, no immediate user-facing outage.
  - Response in business hours.

## Trigger Rules

- Automatic escalation to Sev1:
  - Gateway 5xx > 5% for 10 minutes.
  - Auth or Catalog unavailable > 3 consecutive checks.
  - Dapr sidecar unhealthy for core services > 5 minutes.

## Communication

- Incident channel must include:
  - current severity
  - affected scope
  - mitigation actions
  - next update time
