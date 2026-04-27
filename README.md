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
2. 准备库存与目录（Sprint C：支持 SKU；库存键可用 `productId::skuId`）：

```bash
curl -X PUT http://localhost:5009/api/inventory/p-100::p-100-red-128 -H "Content-Type: application/json" -d "{\"quantity\":100}"
curl -X PUT http://localhost:5009/api/inventory/p-200 -H "Content-Type: application/json" -d "{\"quantity\":100}"
curl -X PUT http://localhost:5008/api/catalog/items/p-100 -H "Content-Type: application/json" -d "{\"name\":\"Demo A\",\"price\":50,\"isActive\":true,\"shopId\":\"shop-east\",\"skus\":[{\"skuId\":\"p-100-red-128\",\"name\":\"Red/128G\",\"price\":56,\"isActive\":true}]}"
curl -X PUT http://localhost:5008/api/catalog/items/p-200 -H "Content-Type: application/json" -d "{\"name\":\"Demo B\",\"price\":80,\"isActive\":true,\"shopId\":\"shop-west\"}"
```

3. **登录、收货地址、购物车结账（Sprint B + Sprint C SKU）**：购物车与结账从 JWT 的 `NameIdentifier` 绑定用户，**不可**再在路径或 body 里冒充他人 `userId`。结账必须传 **`addressId`**，OrderService 会通过 Dapr 调用 UserService 拉取地址并写入订单快照（`shipTo*` 等字段）。行项目支持可选 `skuId`，有 `skuId` 时按 SKU 价格和 SKU 库存扣减。

```bash
curl -X POST http://localhost:5005/api/users/seed
curl -s -X POST http://localhost:5006/api/gw/auth/login -H "Content-Type: application/json" -d "{\"account\":\"demo\",\"password\":\"demo123\"}"
# 从上一行 JSON 取出 data.accessToken 赋给 ACCESS_TOKEN

curl -X POST http://localhost:5006/api/gw/users/me/addresses -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"receiverName\":\"张三\",\"phone\":\"13800000000\",\"region\":\"上海市\",\"detail\":\"XX路1号\",\"isDefault\":true}"
# 从响应 data.id 得到 ADDRESS_ID（UUID）

curl -X PUT http://localhost:5006/api/gw/carts/me -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"lines\":[{\"productId\":\"p-100\",\"skuId\":\"p-100-red-128\",\"quantity\":1},{\"productId\":\"p-200\",\"quantity\":2}]}"
curl -X POST http://localhost:5006/api/gw/orders/checkout -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"promoCode\":\"WELCOME10\",\"addressId\":\"ADDRESS_ID\"}"
```

成功后会清空当前用户购物车。订单会先进入 **`AwaitingPayment`**（库存已预占），`paymentDueAt` 由 `Order:PaymentTimeoutMinutes`（默认 30 分钟）决定。

3b. **模拟支付（Sprint A）**：将响应中的订单 `id` 代入 `orderId`。需携带同一用户的 Bearer；可选 `idempotencyKey`。

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/pay -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"idempotencyKey\":\"demo-pay-1\"}"
```

超时关单（释放库存、主单 `Cancelled`，并发布 `order.cancelled`）：

```bash
curl -X POST "http://localhost:5001/api/orders/ops/expire-awaiting-payments?maxAgeMinutes=30"
```

也可由 **JobService** 触发（容器内已配置 `Jobs__OrderServiceBaseUrl` 指向 OrderService；定时调度器可周期性 `POST`）：

```bash
curl -X POST "http://localhost:5011/api/jobs/run/expire-awaiting-payments?maxAgeMinutes=30"
```

4. **单笔下单（支持 SKU）**：需 JWT，`userId` 以令牌为准（body 中的 `userId` 已忽略）。

```bash
curl -X POST http://localhost:5006/api/gw/orders -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"productId\":\"p-100\",\"skuId\":\"p-100-red-128\",\"quantity\":2,\"promoCode\":\"WELCOME10\"}"
```

4b. **查询增强（Sprint D）**：

- 订单检索：`/api/gw/orders/me/search` 与 `/api/gw/orders/by-user/{userId}/search`（admin）支持 `status`、`productId`、`skuId`、`from`、`to`、`page`、`pageSize`。
- 目录检索：`/api/gw/catalog/items` 支持 `q`、`shopId`、`skuId`、`isActive`、`page`、`pageSize`。

```bash
curl "http://localhost:5006/api/gw/orders/me/search?status=AwaitingPayment&productId=p-100&skuId=p-100-red-128&page=1&pageSize=20" -H "Authorization: Bearer ACCESS_TOKEN"
curl "http://localhost:5006/api/gw/catalog/items?q=Demo&shopId=shop-east&skuId=p-100-red-128&page=1&pageSize=20" -H "Authorization: Bearer ACCESS_TOKEN"
```

4c. **售后申请（Sprint E）**：订单拥有者可发起售后申请，管理员可审批（通过/驳回）。支持按整单或子单维度创建申请。

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/after-sales -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"subOrderId\":\"<subOrderId>\",\"reason\":\"damaged package\",\"detail\":\"box broken\",\"requestedAmount\":20}"
curl http://localhost:5006/api/gw/orders/<orderId>/after-sales -H "Authorization: Bearer ACCESS_TOKEN"

# admin review
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/after-sales/<afterSaleId>/approve -H "Authorization: Bearer ADMIN_ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"note\":\"approved\"}"
```

Sprint E 事件已接入下游：

- `order.aftersale.requested`：Audit 记审计、Notification 发运维通知
- `order.aftersale.reviewed`：Audit 记审批结果、Notification 发审批通知

4d. **支付/退款资金流占位（Sprint F）**：

- 支付：`/api/orders/{orderId}/pay` 通过 `IPaymentGateway` 抽象执行 capture，写入 `paymentTransactionId`（当前为模拟网关实现）。
- 支付回调骨架：新增 `POST /api/orders/payments/callback`，支持 `x-payment-signature` + `x-payment-timestamp` 的 HMAC 校验与回调幂等（`callbackId`）。
- 回调失败分支：`status=failed/cancelled` 时会释放预占库存、将订单置为 `Failed`，并发布 `order.payment.failed`（同时发布 `order.cancelled` 便于兼容下游）。
- 退款：售后单审批通过且 `requestedAmount > 0` 时自动触发 refund，写入售后单 `refundStatus/refundTransactionId/refundedAmount/refundedAt`。
- 退款事件：发布 `order.refunded`，由 Audit/Notification 消费。

4e. **退款幂等 + 对账任务（Sprint G）**：

- 退款幂等：按 `orderId + afterSaleId` 记录退款结果；审批接口重复请求会复用已落库退款流水，避免重复调用退款网关。
- 审批幂等：已审批为 `Approved` 的售后单再次执行 approve 返回当前结果（幂等成功），避免前端重试误报冲突。
- 对账任务：新增 `POST /api/orders/ops/reconcile-refunds?take=200`（admin）扫描退款台账与订单售后状态一致性。
- Gateway/Job 触发：可通过 `POST /api/gw/jobs/run/reconcile-refunds?take=200` 启动对账，并在 Job runs 中查看结果摘要。

4f. **库存预留过期回收（Sprint G.3）**：

- 库存预留台账：`reserve` 操作会记录预留条目（默认 30 分钟 TTL，可传 `ttlMinutes` 覆盖）。
- 正常释放：`release` 时会优先匹配 `reservationId`，无 id 时按最早到期优先标记释放，减少悬挂预留。
- 过期回收接口：`POST /api/inventory/ops/reclaim-expired-reservations?take=200` 扫描预留台账，将超时未释放数量回补库存。
- Gateway/Job 触发：`POST /api/gw/jobs/run/reclaim-expired-inventory-reservations?take=200`，可定时执行并在 Job runs 查看结果。

4g. **账号状态联动（PRD 安全控制补齐）**：

- 订单域关键写操作（购物车改写、下单、支付、发起售后）会通过 UserService 内部接口校验用户状态。
- 仅 `status=active` 允许继续执行；冻结/禁用账号会返回 403（`user_disabled`）。

订单返回中含主单 `Status`（`Pending` / **`AwaitingPayment`** / `Confirmed` / `Completed` / `Cancelled` / `Failed`）、`paymentDueAt` / `paidAt`、失败原因（失败时）、金额（`OriginalAmount` / `DiscountAmount` / `FinalAmount`），以及 `subOrders`（按 `shopId` 拆分；子单 `fulfillmentStatus`：`PendingShipment` / `Shipped` / `Delivered` / `Cancelled`）。

查询单笔（须为订单所有者或 **admin** JWT）：

```bash
curl http://localhost:5006/api/gw/orders/<orderId> -H "Authorization: Bearer ACCESS_TOKEN"
```

**我的订单**（`take` 默认 20，最大 100）：

```bash
curl "http://localhost:5006/api/gw/orders/me?take=10" -H "Authorization: Bearer ACCESS_TOKEN"
```

按指定用户列单（仅 **admin** 网关策略）：

```bash
curl "http://localhost:5006/api/gw/orders/by-user/<userId>?take=10" -H "Authorization: Bearer ADMIN_ACCESS_TOKEN"
```

5. 查看事件处理结果（含本机订阅的 `order.created` / **`order.paid`** / `order.cancelled` / `order.completed` 等 topic 记录；下游 Notification/Audit 亦会消费对应 topic）：

```bash
curl http://localhost:5001/demo/events
```

6. 查看配置快照：

```bash
curl http://localhost:5001/demo/config
```

7. **子单履约（示例）**：主单须先为 **`Confirmed`（已模拟支付）**，每个子单依次 `PendingShipment` → `Shipped` → `Delivered`；全部子单送达后主单变为 `Completed`。将 `order.id` 与各 `order.subOrders[i].id` 代入：

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/ship -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"trackingNumber\":\"SF123\"}"
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/deliver -H "Authorization: Bearer ACCESS_TOKEN"
```

（第二个店铺子单重复上述两条，直到主单 `Status` 为 `Completed`。）

8. **取消（未发货 / 未付款）**：整单取消要求主单为 **`AwaitingPayment` 或 `Confirmed`**，且**所有**子单仍为 `PendingShipment`，会按行调用库存 `release`。单个子单取消仅释放该子单行；若全部子单被取消则主单变为 `Cancelled`。已发货/已送达的主单或子单不可取消（需走售后流程时再扩展）。

```bash
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/cancel -H "Authorization: Bearer ACCESS_TOKEN"
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/cancel -H "Authorization: Bearer ACCESS_TOKEN"
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
