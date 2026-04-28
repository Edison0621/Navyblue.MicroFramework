# Service Ownership

## Ownership Model

- Every service must have:
  - primary owner
  - backup owner
  - on-call group
  - escalation path

## Service Map

- `gatewayservice`: Platform API team
- `authservice`: Identity team
- `userservice`: Identity team
- `catalogservice`: Commerce catalog team
- `orderservice`: Commerce order team
- `inventoryservice`: Commerce inventory team
- `promotionservice`: Commerce promotion team
- `jobservice`: Platform operations team
- `opsportal`: Platform operations team

## Escalation

- Layer 1: service owner
- Layer 2: domain backup
- Layer 3: platform incident commander

## Ownership Checklist

- owner rotation maintained
- service runbook maintained
- alert subscriptions validated
- SLO reviewed monthly
