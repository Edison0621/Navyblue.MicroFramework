# Dual-Host Deployment with Consul (No K8s)

This folder provides a practical Docker deployment split for:

- Infra node: Consul + Redis + Jaeger + OTel collector
- Order node: OrderService + Dapr sidecar + Consul agent
- Product node: ProductService (+ canary) + Dapr sidecars + Consul agent

## 1) Infra node

Use:

- `deploy/infra/docker-compose.infra.yml`
- `deploy/infra/.env.example` (copy to `.env` first)

Start:

```powershell
docker compose --env-file .env -f docker-compose.infra.yml up -d --build
```

## 2) Order node

Use:

- `deploy/order-node/docker-compose.order.yml`
- `deploy/order-node/.env.example` (copy to `.env` first)

Start:

```powershell
docker compose --env-file .env -f docker-compose.order.yml up -d --build
```

## 3) Product node

Use:

- `deploy/product-node/docker-compose.product.yml`
- `deploy/product-node/.env.example` (copy to `.env` first)

Start:

```powershell
docker compose --env-file .env -f docker-compose.product.yml up -d --build
```

## Notes

- Dapr components in each node point to `redis.infra.local:6379`.
- `extra_hosts` maps `${CONSUL_HOST}`, `${REDIS_HOST}`, `${OTEL_HOST}` to `${INFRA_HOST_IP}`.
- Consul agent joins the server at `${CONSUL_SERVER_IP}`.
- `gliderlabs/registrator` auto-registers Docker services into Consul.
- Host ports (`ORDER_PORT`, `PRODUCT_PORT`, `PRODUCT_CANARY_PORT`) can be adjusted.
