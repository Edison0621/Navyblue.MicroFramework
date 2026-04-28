# Navyblue · DaprFx

**语言：** [English](./README.md) | 简体中文

## 项目概览

**DaprFx** 是一套面向 **.NET**（示例基于 **.NET 10**）、构建于 **[Dapr](https://dapr.io/)** 之上的轻量级微服务**工具集与参考实现**。目标是在不绑定单一厂商运行时的情况下，提供接近 **Spring Cloud** 的开发体验：类型化抽象、弹性调用、事件总线、Outbox、状态存储与可观测性挂钩。

本仓库包含：**框架库**（`src/DaprFx.*`）、**领域示例服务**（`samples/*`）、**Docker Compose 编排**、**可选可观测性栈**、以及 **React 前端**，用于端到端演示与二次开发基线。

---

## 核心能力

| 领域 | 说明 |
|------|------|
| **服务调用** | 面向接口的动态代理；调用级重试、超时、熔断 |
| **客户端拓扑** | 多 `appId` 注册与 **RoundRobin / Random / Sticky** 负载均衡 |
| **消息** | Dapr Pub/Sub 与 **Outbox** 可靠投递 |
| **韧性** | 幂等存储、死信处理、可配置退避 |
| **状态** | 泛型 Dapr 状态存储抽象与仓储模式示例 |
| **配置** | Dapr Configuration Provider 与安全刷新 |
| **安全** | 加解密抽象；示例服务采用 JWT 服务间身份模型 |
| **网关** | BFF 式转发、限流、结构化错误体 |

---

## 仓库结构

| 路径 | 职责 |
|------|------|
| `src/DaprFx.Core` | 核心抽象、选项与基础类型 |
| `src/DaprFx.ServiceInvocation` | 动态调用代理与策略 |
| `src/DaprFx.EventBus` | 事件总线、Outbox、幂等与死信 |
| `src/DaprFx.StateManagement` | 类型化状态访问 |
| `src/DaprFx.Configuration` | 配置 Provider 与刷新 |
| `src/DaprFx.Cryptography` | 加解密实现 |
| `src/DaprFx.Hosting` | Hosting / DI 一站式扩展 |
| `src/DaprFx.Operations` | Ops 看板扩展点（示例） |
| `samples/*Service` | 订单、目录、网关、认证等参考微服务 |
| `components/` | Dapr 组件（Redis 状态、Pub/Sub 等） |
| `deploy/` | 多节点 / 基础设施向的 Compose 片段 |
| `observability/` | Prometheus、Grafana、Loki、Alertmanager、Collector 配置 |
| `docs/ops/` | SLO、事故分级、Runbook、演练与复盘模板 |
| `scripts/` | 本地生命周期脚本（`dev-up.ps1`、`dev-down.ps1` 等） |

---

## 环境要求

- **Docker Desktop**（或兼容引擎）与 **Compose V2**
- **.NET SDK 10**（容器外本地编译）
- **Node.js 20+**（前端开发）
- 宿主机默认占用端口：**5000–5013**、**6379**、**4317**、**16686** 等需可用

---

## 快速开始（Docker Compose）

### Windows（推荐）

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1
```

镜像已就绪时可跳过构建：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1 -NoBuild
```

停止：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-down.ps1
```

脚本会预拉取 .NET 基础镜像、执行 `docker compose up`，并对关键 HTTP 端点做**可达性探测**。

### 任意平台

```bash
docker compose up --build -d
docker compose ps
```

```bash
docker compose down
```

---

## 运行模式

| 模式 | 适用场景 | 说明 |
|------|----------|------|
| **Compose 默认** | 单机联调；Redis / Jaeger / Collector 作为容器服务 | `docker-compose.yml` + `components/` |
| **localhost 组件** | 宿主机 `dotnet run` + 本机 Redis | `components/local`，见 `scripts/dev-local.ps1` |
| **类生产多机** | 域名访问 Consul / Redis / OTLP | `components/cluster`、`deploy/*`，见 `scripts/dev-cluster.ps1` |

集群模式请按节点准备 `.env`（模板见 `.env.example`）。常见变量：`CONSUL_HOST`、`REDIS_HOST`、`OTEL_HOST`、`INFRA_HOST_IP`。

---

## 服务与端口（默认宿主机映射）

| 服务 | 地址 | 说明 |
|------|------|------|
| Ops Portal | http://localhost:5000 | 聚合运维入口 |
| OrderService | http://localhost:5001 | 订单、Outbox、支付示例 |
| ProductService | http://localhost:5002 | 商品示例（被调方） |
| ProductService Canary | http://localhost:5003 | 灰度实例 |
| AuthService | http://localhost:5004 | 登录、刷新、JWT |
| UserService | http://localhost:5005 | 用户、地址、内部校验 |
| **GatewayService** | **http://localhost:5006** | **对外 BFF / 统一入口** |
| AuditService | http://localhost:5007 | 审计事件 |
| CatalogService | http://localhost:5008 | 商品目录与治理 |
| InventoryService | http://localhost:5009 | 库存与预留 |
| NotificationService | http://localhost:5010 | 通知 |
| JobService | http://localhost:5011 | 运维任务 / 对账类 Job |
| PromotionService | http://localhost:5012 | 营销活动 |
| **Platform Admin** | http://localhost:5013 | 平台管理后台（静态资源 / Nginx） |
| Jaeger UI | http://localhost:16686 | 链路追踪 |
| OTLP Collector | grpc://localhost:4317 | 遥测接入 |

**前端（Vite 开发态）：** `frontend/buyer-web`、`frontend/merchant-admin` 等默认 `npm run dev` 在 **5173+** 端口；通过环境变量 `VITE_GATEWAY_BASE_URL=http://localhost:5006` 指向网关。

---

## 可观测性与运维

- **主栈**已包含 **Jaeger**、**OpenTelemetry Collector**（配置见 `observability/otel-collector-config.yaml`）。
- **可选增强栈**（Prometheus / Grafana / Loki / Alertmanager）：

```bash
docker compose -f observability/docker-compose.observability.yml up -d
```

- **运维文档**：`docs/ops/`（SLO 目录、事故分级、服务 Owner、Runbook、演练清单、复盘模板等）。
- **契约回归提示**：`CONTRACT_TEST_CHECKLIST.md`。

---

## 网关统一错误体（联调）

当下游不可达或网关自身失败时，可能返回如下 JSON（非全部 `ApiResponse` 包装）：

```json
{
  "errorCode": "gateway_timeout",
  "message": "Gateway timed out while waiting for downstream service.",
  "detail": "…",
  "correlationId": "…"
}
```

常见 `errorCode`：`gateway_timeout`（504）、`downstream_unavailable` / `downstream_circuit_open`（502/503）、`gateway_forwarding_failed`、`rate_limited`（429）、`client_blocked`（403）。

**建议：** 请求携带 `x-correlation-id`，并结合 **Jaeger** / 容器日志做跨服务关联。

---

## 本地开发（仅 `dotnet run`）

若不在 Compose 内运行，请自行保证：

- Dapr Sidecar 已启动（如 `dapr run …`），且与 `Dapr:GrpcEndpoint` / `Dapr:HttpEndpoint` 配置一致；
- **Redis** 与 `components/*` 中的地址一致（容器网络多为 `redis:6379`，宿主机多为 `localhost:6379`）。

否则易出现 gRPC **连接被拒绝**（如 Windows 错误码 10061）。

---

## API 响应约定（示例服务）

多数示例 HTTP API 使用统一信封：

**成功：** `{ "success": true, "data": <载荷>, "error": null, "traceId"?: "…" }`  
**失败：** `{ "success": false, "data": null, "error": { "code", "message", "details?" }, "traceId"?: "…" }`

常见 `error.code`：`not_found`、`invalid_request`、`unauthorized`、`conflict`、`upstream_error`、`internal_error`、`insufficient_inventory`、`order_creation_failed` 等。C# 中请优先使用各服务 **`ApiErrorCodes.*`** 常量，避免魔法字符串。

**分页：** 查询参数 `page`、`pageSize`；`data` 内通常含 `items`、`page`、`pageSize`、`total`（默认 `page=1`，`pageSize` 上限多为 **200**）。

**例外：** `POST /api/promotions/validate` 返回裸 **`PromotionValidationResult`** JSON，便于 Dapr `Invoke` 等场景直接反序列化。

**网关 GET：** 对显式 `ForwardGet` 路由，会将入站 QueryString 透传拼接到上游 URL。

---

## 最小验证

```bash
curl -X POST http://localhost:5005/api/users/seed
curl -X POST http://localhost:5005/api/users/seed-admin
curl -s -X POST http://localhost:5006/api/gw/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"account\":\"demo\",\"password\":\"demo123\"}"
```

将响应中 `data.accessToken` 作为后续 `Authorization: Bearer …` 调用 `http://localhost:5006/api/gw/...`。

---

## 前端应用

| 路径 | 说明 |
|------|------|
| `frontend/buyer-web` | 买家商城（React + Vite + TypeScript） |
| `frontend/merchant-admin` | 商家后台（订单、商品、营销等） |
| `frontend/platform-admin` | 平台运营后台（用户治理、审核等；生产镜像映射 **5013**） |

```bash
cd frontend/buyer-web
npm install && npm run dev
```

更多：`frontend/buyer-web/docs/`、`docs/buyer-management-contract.md`、`docs/prd-closure-status.md`。

---

## 附录 A：端到端联调与场景命令（详尽）

以下保留历史版本中的 **Sprint / 场景化 curl 编排**、网关安全、各域快速验证等，供深度联调与培训使用。

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

4h. **商家子单最小权限（PRD 商家侧能力补齐）**：

- 子单操作（发货/签收/子单取消）支持商家角色访问：JWT `roles` 中包含 `shop:<shopId>`（或 `shop-manager:<shopId>`）即可管理对应店铺子单。
- 管理员仍可操作全部子单；订单买家也可操作自己的子单，且无法跨店操作其他商家子单。
- 商家订单视图：新增 `GET /api/orders/by-shop/{shopId}` 与 `GET /api/orders/by-shop/{shopId}/search`，返回店铺视角子单列表（支持 `orderStatus`、`subOrderStatus`、`productId`、`skuId`、时间范围和分页过滤）。
- 商家售后工作台：新增 `GET /api/orders/by-shop/{shopId}/after-sales` 与 `GET /api/orders/by-shop/{shopId}/after-sales/search`，支持按 `status`、`refundStatus`、时间范围和分页筛选。

4i. **商品域后台 + 物流跟踪闭环（PRD full_plus + carrier_adapter）**：

- 类目后台：新增类目树 CRUD（创建/更新、排序、显示、启停），并支持类目停用联动下架（同类目在架商品自动下架）。
- 类目治理增强：支持类目删除（存在子类目或挂载商品时拒绝删除）；类目停用时会对子树类目递归联动下架。
- 审核流：商品支持提审、通过、驳回、审核历史；上架前置 `AuditStatus=Approved`。
- 权限收口：商品写操作（改商品、提审、上下架、定时上下架）要求 admin 或店铺归属商家（`shop:<shopId>` / `shop-manager:<shopId>`）。
- 上下架与定时：支持即时上/下架与定时窗口（`onAt/offAt`），提供 `POST /api/gw/catalog/ops/apply-shelf-schedules` 执行幂等调度。
- 下单规则收口：OrderService 下单校验增加“审核通过 + 当前可上架 + 类目可用”约束，并对旧数据启用兼容回退（历史仅 `isActive` 数据可继续交易）。
- 物流轨迹：子单新增 `carrierCode/carrierName/trackingNumber` 与轨迹事件（`CreatedAt/Status/Message/Source`），发货/签收必落轨迹，并提供 `GET /api/gw/orders/{orderId}/sub-orders/{subOrderId}/tracking` 查询。
- 观测事件：新增并发布 `order.shipped`、`order.delivered`，Audit/Notification 已订阅消费。

4j. **生产骨架能力边界（可扩展点）**：

- 支付网关：`IPaymentGateway` 结果结构补齐了 `errorCode/retryable/gateway/gatewayTransactionId`，用于后续接真实 PSP 的错误分层与重试策略。
- 物流网关：`ShipmentTracking` 增加 provider 配置（provider/timeout/retry/deduplicate/source），默认 `mock`，便于替换真实承运商实现。
- 调度运维：类目定时上下架支持直接 ops 触发与 Job 触发两种入口，执行结果统一落 Job run 记录。

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
curl -X POST http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/ship -H "Authorization: Bearer ACCESS_TOKEN" -H "Content-Type: application/json" -d "{\"trackingNumber\":\"SF123\",\"carrierCode\":\"SF\",\"carrierName\":\"ShunFeng\"}"
curl http://localhost:5006/api/gw/orders/<orderId>/sub-orders/<subOrderId>/tracking -H "Authorization: Bearer ACCESS_TOKEN"
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

> **说明：** API 信封、错误码与分页约定已上移至正文「API 响应约定」一节，此处不再重复。

## 契约测试清单

- 最小契约测试清单见：`CONTRACT_TEST_CHECKLIST.md`

## 前端补充文档

- `frontend/buyer-web/docs/api-gap-checklist.md`
- `frontend/buyer-web/docs/release-checklist.md`
- `docs/buyer-management-contract.md`
- `docs/prd-closure-status.md`

---

## 使用声明

本仓库用于**学习、演示与二次开发基线**。若用于生产环境，请自行完成密钥与配置治理、容量与弹性规划、安全审计、SLO/告警及值班流程等配套建设；示例中的默认密钥与宽松限流**不可**直接用于生产。
