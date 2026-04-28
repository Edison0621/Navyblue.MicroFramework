# Runbook: Deadletter Backlog Growth

## Trigger

- Deadletter count increases continuously or exceeds threshold.

## Investigation

1. Check outbox/deadletter metrics from OpsPortal and logs.
2. Identify event type with highest failure count.
3. Confirm target subscriber health.

## Mitigation

1. Restore subscriber availability.
2. Replay deadletter batches in controlled size.
3. Validate replay success and idempotency.

## Guardrails

- Do not bulk replay without rate control.
- Keep audit trail of replay command and operator.
