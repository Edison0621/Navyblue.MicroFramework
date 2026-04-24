# Contributing

## Coding Rules

- One top-level type per file: each `.cs` file should contain exactly one class, interface, enum, or record.
- Keep namespace stable during refactors unless there is an explicit migration plan.
- Add or update tests for any behavior change.
- Run both commands before submitting changes:
  - `dotnet build DaprFx.slnx`
  - `dotnet test DaprFx.slnx`

## Testing Focus

Prioritize tests around:

- service invocation retry/timeout/circuit-breaker behavior
- outbox retry/backoff/dead-letter behavior
- idempotency key and TTL behavior
- configuration refresh behavior
