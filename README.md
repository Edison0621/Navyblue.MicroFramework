# DaprFx

[English](./README.en.md) | 中文

`DaprFx` 是一个构建在 Dapr 之上的轻量级 .NET 微服务框架（当前示例基于 `.NET 10`）。
它提供类似 Spring Cloud 的开发体验，重点能力包括：

- 接口代理式服务调用（service invocation）
- 调用级重试 / 超时 / 熔断
- 多 appId 客户端注册与负载均衡（RoundRobin/Random/Sticky）
- 基于 Dapr 的事件发布订阅
- Outbox 可靠投递、幂等与死信处理
- 泛型状态存储抽象
- Dapr 配置中心接入与动态刷新
- 加解密服务抽象

## 目录结构

- `src/DaprFx.Core`：核心抽象与选项定义
- `src/DaprFx.ServiceInvocation`：动态调用代理实现
- `src/DaprFx.EventBus`：事件总线、Outbox、死信能力
- `src/DaprFx.StateManagement`：类型化状态存储
- `src/DaprFx.Configuration`：配置中心 provider 与刷新机制
- `src/DaprFx.Cryptography`：密码学抽象实现
- `src/DaprFx.Hosting`：一站式扩展（DI + Hosting）
- `samples/OrderService`：订单服务示例（调用方）
- `samples/ProductService`：商品服务示例（被调方）

## 快速开始（推荐：Docker Compose）

### 1) 一键启动（Windows PowerShell）

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1
```

可选：若镜像已就绪，跳过 build：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1 -NoBuild
```

### 2) 一键停止

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-down.ps1
```

### 3) 运行模式说明（localhost / 生产）

#### A. localhost 开发模式（单机调试）

适用于：你在一台开发机上调试服务，希望 Redis 等组件直接使用 `localhost`。

- 组件目录：`components/local`（`redisHost=localhost:6379`）
- 启动命令：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-local.ps1
```

- 停止命令：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-local-down.ps1
```

#### B. 生产/多机域名模式（推荐部署）

适用于：订单与商品分机器部署，通过域名访问基础设施（Consul/Redis/OTel）。

- 组件目录：`components/cluster` 与 `deploy/*/components`（`redisHost=redis.infra.local:6379`）
- 先准备各节点 `.env`（以 `.env.example` 为模板）
- 启动命令（按节点执行）：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-cluster.ps1 -Node infra
powershell -ExecutionPolicy Bypass -File .\scripts\dev-cluster.ps1 -Node order
powershell -ExecutionPolicy Bypass -File .\scripts\dev-cluster.ps1 -Node product
```

- 关键环境变量：
  - `CONSUL_HOST`（如 `consul.infra.local`）
  - `REDIS_HOST`（如 `redis.infra.local`）
  - `OTEL_HOST`（如 `otel.infra.local`）
  - `INFRA_HOST_IP`（域名映射目标 IP）

### 4) 访问地址

- OrderService: [http://localhost:5001](http://localhost:5001)
- ProductService: [http://localhost:5002](http://localhost:5002)
- ProductService Canary: [http://localhost:5003](http://localhost:5003)
- AuthService: [http://localhost:5004](http://localhost:5004)
- UserService: [http://localhost:5005](http://localhost:5005)
- GatewayService: [http://localhost:5006](http://localhost:5006)
- AuditService: [http://localhost:5007](http://localhost:5007)
- CatalogService: [http://localhost:5008](http://localhost:5008)
- InventoryService: [http://localhost:5009](http://localhost:5009)
- NotificationService: [http://localhost:5010](http://localhost:5010)
- JobService: [http://localhost:5011](http://localhost:5011)
- PromotionService: [http://localhost:5012](http://localhost:5012)
- Ops Portal: [http://localhost:5000](http://localhost:5000)
- Jaeger UI: [http://localhost:16686](http://localhost:16686)

## 手动启动方式（不使用脚本）

```bash
docker compose up --build -d
docker compose ps
```

停止：

```bash
docker compose down
```

## 本地开发（仅 dotnet run）说明

如果你直接 `dotnet run`，请确保以下依赖已就绪，否则会出现 Dapr gRPC 连接失败（如 10061）：

- Dapr sidecar 正在运行（`dapr run ...`）
- Redis / 配置组件可达
- 组件地址与运行环境匹配（容器内一般是 `redis:6379`，宿主机通常是 `localhost:6379`）

## 5 分钟体验流程

1. 启动整套环境（推荐脚本或 compose）。
2. 先准备库存：

```bash
curl -X PUT http://localhost:5009/api/inventory/p-100 -H "Content-Type: application/json" -d "{\"quantity\":100}"
```

3. 创建订单（触发库存预扣、状态写入、服务调用、事件发布）：

```bash
curl -X POST http://localhost:5001/api/orders -H "Content-Type: application/json" -d "{\"productId\":\"p-100\",\"quantity\":2,\"promoCode\":\"WELCOME10\"}"
```

订单返回中包含 `Status`（`Pending/Confirmed/Failed`）、失败原因（失败时）以及金额字段（`OriginalAmount` / `DiscountAmount` / `FinalAmount`）。

可通过以下接口查询订单最终状态：

```bash
curl http://localhost:5001/api/orders/<orderId>
```

4. 查看事件处理结果：

```bash
curl http://localhost:5001/demo/events
```

5. 查看配置快照：

```bash
curl http://localhost:5001/demo/config
```

## 常用运维/排障命令

```bash
docker compose ps -a
docker compose logs -f orderservice orderservice-dapr
docker compose logs -f productservice productservice-dapr
docker compose logs -f otel-collector jaeger
```

如果拉镜像偶发失败（EOF/timeout）可重试：

```bash
docker pull mcr.microsoft.com/dotnet/aspnet:10.0
docker pull mcr.microsoft.com/dotnet/sdk:10.0
```

### Gateway 统一错误返回（联调建议）

当网关无法正常转发到下游服务时，会返回统一错误结构：

```json
{
  "errorCode": "gateway_timeout",
  "message": "Gateway timed out while waiting for downstream service.",
  "detail": "...",
  "correlationId": "..."
}
```

常见 `errorCode`：

- `gateway_timeout`：网关等待下游超时（HTTP 504）
- `downstream_unavailable`：网关无法连接下游（HTTP 502）
- `gateway_forwarding_failed`：网关转发过程异常（HTTP 502）
- `rate_limited`：触发网关限流（HTTP 429）
- `client_blocked`：命中网关黑名单（HTTP 403）

排障建议：

- 带上 `x-correlation-id` 调用 Gateway，便于跨服务串联日志
- 重点查看 `gatewayservice` 与对应下游服务容器日志
- 结合 `traceparent` / Jaeger 链路定位慢点与失败点

## Dapr 组件

`components/` 目录包含示例组件：

- `pubsub.yaml` -> `orderpubsub`
- `statestore.yaml` -> `statestore`
- `appconfig.yaml` -> `appconfig`

默认示例依赖 Redis。

## 核心配置项（节选）

- `InvocationMaxRetries` / `InvocationTimeoutSeconds` / `CircuitBreaker*`
- `LoadBalancingStrategy`（`RoundRobin` / `Random` / `Sticky`）
- `OutboxMaxRetryCount` / `OutboxBaseDelaySeconds` / `OutboxDeadLetterStateKey`
- `IdempotencyTtlMinutes`
- `OtlpEndpoint` / `TelemetryServiceName` / `TelemetryEnvironment`

## Gateway 防刷配置建议（Dev / Prod）

`GatewayService` 已内置可热更新的防刷配置，建议按环境给默认值：

- Dev（便于联调，默认 `appsettings.json`）
  - `auth`: `20 req / 60s`
  - `write`: `30 req / 60s`
  - `read`: `120 req / 60s`
  - 日志级别：`Information`
- Prod（更严格，`appsettings.Production.json`）
  - `auth`: `8 req / 60s`
  - `write`: `20 req / 60s`
  - `read`: `80 req / 60s`
  - 日志级别：`Warning`

切换生产配置方式：

```bash
# docker-compose 环境变量（Gateway 服务）
ASPNETCORE_ENVIRONMENT=Production
```

上线后建议先使用 `GET /api/gw/security/overview` 与 `GET /api/gw/security/rate-limit-metrics?take=20` 观察 1-2 天，再按业务峰值微调策略。

## 健康检查与接口

- 健康检查：`/health/live`、`/health/ready`
- 死信查询（示例）：`GET /ops/outbox/deadletters?page=1&pageSize=100`
- 死信重放（示例）：`POST /ops/outbox/deadletters/replay-all?dryRun=true`

## 新增服务（M1 骨架）快速验证

1. 登录获取 JWT：

```bash
curl -X POST http://localhost:5004/api/auth/login -H "Content-Type: application/json" -d "{\"account\":\"demo\",\"password\":\"demo123\"}"
```

2. 通过 Gateway 调用受保护接口（将 `TOKEN` 替换为上一步 `accessToken`）：

```bash
curl http://localhost:5006/api/gw/orders/o-100 -H "Authorization: Bearer TOKEN"
```

注册账号（通过 Gateway）：

```bash
curl -X POST http://localhost:5006/api/gw/auth/register -H "Content-Type: application/json" -d "{\"username\":\"alice\",\"email\":\"alice@example.com\",\"password\":\"Passw0rd!\"}"
```

3. 初始化并读取用户（UserService 通过 Dapr `statestore` 持久）：

```bash
curl -X POST http://localhost:5005/api/users/seed
curl -X POST http://localhost:5005/api/users/seed-admin
curl "http://localhost:5005/api/users?page=1&pageSize=50"
```

4. 查询审计（AuditService 可接收 `order.created` 主题并写入 `statestore`）：

```bash
curl "http://localhost:5007/api/audit/events?page=1&pageSize=20&action=order.created"
```

5. 新增服务快速验证：

```bash
# Catalog
curl -X PUT http://localhost:5008/api/catalog/items/p-200 -H "Content-Type: application/json" -d "{\"name\":\"Keyboard\",\"price\":199.0,\"isActive\":true}"
curl "http://localhost:5008/api/catalog/items?page=1&pageSize=50"

# Inventory
curl -X PUT http://localhost:5009/api/inventory/p-200 -H "Content-Type: application/json" -d "{\"quantity\":50}"
curl -X POST http://localhost:5009/api/inventory/p-200/reserve -H "Content-Type: application/json" -d "{\"quantity\":2}"

# Notification
curl -X POST http://localhost:5010/api/notifications/send -H "Content-Type: application/json" -d "{\"channel\":\"email\",\"to\":\"demo@example.com\",\"title\":\"Hi\",\"body\":\"Welcome\"}"
curl "http://localhost:5010/api/notifications?page=1&pageSize=20"
# 若出现库存补偿失败，会收到 "Order Compensation Failed" 告警通知

# Job
curl -X POST http://localhost:5011/api/jobs/run/reconcile-inventory
curl "http://localhost:5011/api/jobs/runs?page=1&pageSize=20"

# Promotion
curl -X POST http://localhost:5012/api/promotions -H "Content-Type: application/json" -d "{\"code\":\"WELCOME10\",\"name\":\"Welcome Campaign\",\"discountType\":\"percentage\",\"discountValue\":10,\"startAt\":\"2026-01-01T00:00:00Z\",\"endAt\":\"2026-12-31T23:59:59Z\",\"isEnabled\":true}"
curl "http://localhost:5012/api/promotions?page=1&pageSize=20"
curl -X POST http://localhost:5012/api/promotions/validate -H "Content-Type: application/json" -d "{\"code\":\"WELCOME10\",\"orderAmount\":299}"
```

6. Gateway 路由验证（需先登录拿到 `TOKEN`）：

```bash
# catalog (admin 写, 登录用户读)
curl http://localhost:5006/api/gw/catalog/items -H "Authorization: Bearer TOKEN"
curl -X PUT http://localhost:5006/api/gw/catalog/items/p-300 -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d "{\"name\":\"Mouse\",\"price\":99.0,\"isActive\":true}"

# inventory
curl http://localhost:5006/api/gw/inventory/p-200 -H "Authorization: Bearer TOKEN"
curl -X POST http://localhost:5006/api/gw/inventory/p-200/reserve -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d "{\"quantity\":1}"

# jobs/notifications (admin)
curl -X POST http://localhost:5006/api/gw/jobs/run/reconcile-inventory -H "Authorization: Bearer TOKEN"
curl "http://localhost:5006/api/gw/notifications?page=1&pageSize=20" -H "Authorization: Bearer TOKEN"

# orders via gateway (create + query)
curl -X POST http://localhost:5006/api/gw/orders -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d "{\"productId\":\"p-100\",\"quantity\":1,\"promoCode\":\"WELCOME10\"}"
curl http://localhost:5006/api/gw/orders/<orderId> -H "Authorization: Bearer TOKEN"

# promotions via gateway
curl -X POST http://localhost:5006/api/gw/promotions -H "Authorization: Bearer ADMIN_TOKEN" -H "Content-Type: application/json" -d "{\"code\":\"WELCOME10\",\"name\":\"Welcome Campaign\",\"discountType\":\"percentage\",\"discountValue\":10,\"startAt\":\"2026-01-01T00:00:00Z\",\"endAt\":\"2026-12-31T23:59:59Z\",\"isEnabled\":true}"
curl -X POST http://localhost:5006/api/gw/promotions/validate -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d "{\"code\":\"WELCOME10\",\"orderAmount\":299}"
```

7. Gateway 限流与黑名单：

```bash
# 建议在请求头带上 x-client-id，便于按客户端维度限流
curl http://localhost:5006/api/gw/catalog/items -H "Authorization: Bearer TOKEN" -H "x-client-id: demo-client"

# admin 管理黑名单
curl http://localhost:5006/api/gw/security/blacklist -H "Authorization: Bearer ADMIN_TOKEN"
curl -X POST http://localhost:5006/api/gw/security/blacklist -H "Authorization: Bearer ADMIN_TOKEN" -H "Content-Type: application/json" -d "{\"client\":\"client:demo-client\",\"ttlMinutes\":60}"
curl -X DELETE http://localhost:5006/api/gw/security/blacklist/client:demo-client -H "Authorization: Bearer ADMIN_TOKEN"

# admin 查看限流统计
curl "http://localhost:5006/api/gw/security/rate-limit-metrics?take=20" -H "Authorization: Bearer ADMIN_TOKEN"
curl "http://localhost:5006/api/gw/security/rate-limit-metrics/client:demo-client" -H "Authorization: Bearer ADMIN_TOKEN"

# admin 清理统计 / 黑名单
curl -X DELETE "http://localhost:5006/api/gw/security/rate-limit-metrics/client:demo-client" -H "Authorization: Bearer ADMIN_TOKEN"
curl -X POST "http://localhost:5006/api/gw/security/rate-limit-metrics/reset" -H "Authorization: Bearer ADMIN_TOKEN"
curl -X DELETE "http://localhost:5006/api/gw/security/blacklist" -H "Authorization: Bearer ADMIN_TOKEN"

# admin 安全总览
curl "http://localhost:5006/api/gw/security/overview" -H "Authorization: Bearer ADMIN_TOKEN"

# admin 热更新限流策略与豁免路径（无需重启）
curl "http://localhost:5006/api/gw/security/config" -H "Authorization: Bearer ADMIN_TOKEN"
curl -X PUT "http://localhost:5006/api/gw/security/config/rate-limit-policies/auth" -H "Authorization: Bearer ADMIN_TOKEN" -H "Content-Type: application/json" -d "{\"permitLimit\":10,\"windowSeconds\":60,\"queueLimit\":0}"
curl -X PUT "http://localhost:5006/api/gw/security/config/exempt-paths" -H "Authorization: Bearer ADMIN_TOKEN" -H "Content-Type: application/json" -d "{\"paths\":[\"/\",\"/health/live\",\"/health/ready\",\"/api/gw/security/overview\"]}"
```

说明：

- 黑名单会持久化到 `statestore`（Gateway 重启后仍生效）
- 黑名单支持 `ttlMinutes`，到期自动解封
- 限流默认豁免路径可通过 `Security.RateLimitExemptPaths` 配置（默认含健康检查）
- 限流支持按策略分级（`auth` / `write` / `read`），可通过 `Security.RateLimitPolicies` 调整阈值

## API Response Conventions

`audit` / `auth` / `catalog` / `inventory` / `job` / `notification` / `user` / `order` / `product` / `promotion` services now use a unified response style:

- Success shape:
  - `success: true`
  - `data: <payload>`
  - `error: null`
  - `traceId: <optional trace id>`
- Failure shape:
  - `success: false`
  - `data: null`
  - `error: { code, message, details? }`
  - `traceId: <optional trace id>`

Standard error codes:

- `not_found`
- `invalid_request`
- `unauthorized`
- `conflict`
- `upstream_error`
- `internal_error`
- `invalid_response`
- `invalid_quantity`
- `insufficient_inventory`
- `invalid_promotion`
- `inventory_reservation_failed`
- `order_creation_failed`

In C# sample services, use `ApiErrorCodes.*` constants instead of string literals for these codes.

Pagination conventions for list endpoints:

- Query params: `page`, `pageSize`
- Response payload in `data`: `items`, `page`, `pageSize`, `total`
- Current defaults: `page=1`, `pageSize=50`, max `pageSize=200`

Exception (service-to-service): `POST /api/promotions/validate` returns a root JSON object matching `PromotionValidationResult` (fields `valid`, `reason`, `code`, `discountType`, `discountValue`, `orderAmount`, `discountAmount`, `finalAmount`) so Dapr `Invoke` clients (for example OrderService) can deserialize the body directly without an `ApiResponse` wrapper.

Gateway `GET` forwards: incoming query strings are appended to the upstream URL for all explicit `ForwardGet` routes (users, orders, catalog, inventory, promotions, notifications, jobs).
