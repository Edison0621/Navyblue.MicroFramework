# 最小契约测试清单（Catalog / Order / Payment / Tracking）

适用范围：`PRD full_plus + carrier_adapter + prod skeleton` 当前实现。  
执行入口建议：`samples/GatewayService/GatewayService.http`。

---

## 0. 前置准备

- [ ] 启动完整环境（Gateway / Catalog / Order / User / Inventory / Job / Audit / Notification）。
- [ ] 准备 `adminToken`、`token`（普通用户或商家用户）。
- [ ] 准备可用 `addressId`（下单用）。
- [ ] 确认测试商品与类目使用独立 ID（避免污染已有演示数据）。

---

## 1. 类目治理契约

### 1.1 类目删除约束

- [ ] 删除“存在子类目”的类目。
  - 请求：`DELETE /api/gw/catalog/categories/{id}`
  - 期望：`409`
  - 期望错误码：`conflict`
  - 关键语义：`Category has child categories.`

- [ ] 删除“存在挂载商品”的类目。
  - 请求：`DELETE /api/gw/catalog/categories/{id}`
  - 期望：`409`
  - 期望错误码：`conflict`
  - 关键语义：`Category has products.`

- [ ] 删除“无子类目且无挂载商品”的类目。
  - 请求：`DELETE /api/gw/catalog/categories/{id}`
  - 期望：`200`
  - 关键语义：`data.deleted == {id}`

### 1.2 类目停用递归联动

- [ ] 停用父类目后，子树类目下在架商品自动下架。
  - 请求：`PATCH /api/gw/catalog/categories/{id}/enabled` body `{ "isEnabled": false }`
  - 期望：`200`
  - 二次校验：商品查询中 `isOnShelf == false`。

---

## 2. 商品写权限契约

- [ ] 非 admin 且无目标店铺角色，写商品（upsert/submit/shelf/schedule）应被拒绝。
  - 期望：`403`
  - 期望错误码：`forbidden`

- [ ] admin 可写任意店铺商品。
  - 期望：`200`

- [ ] 拥有 `shop:{shopId}` 或 `shop-manager:{shopId}` 的用户仅可写该店铺商品。
  - 同店铺：`200`
  - 跨店铺：`403` + `forbidden`

---

## 3. 下单规则契约（订单侧）

- [ ] 商品未审核通过禁止成交。
  - 期望：`400`
  - 关键语义：`Catalog item is not approved.`

- [ ] 商品未上架禁止成交。
  - 期望：`400`
  - 关键语义：`Catalog item is off shelf.`

- [ ] 商品所在类目不可用（停用联动后）禁止成交。
  - 期望：`400`
  - 关键语义：`Catalog item's category is disabled.`

---

## 4. 支付/退款骨架契约

### 4.1 Capture 失败分层字段

- [ ] 触发支付网关失败（可用 `PaymentGateway:SimulateTransientFailure=true` 环境）。
  - 请求：`POST /api/gw/orders/{orderId}/pay`
  - 期望：`502`
  - 期望错误码：`upstream_error`
  - 期望 details 字段包含：`errorCode`、`retryable`、`gateway`

### 4.2 Refund 失败分层字段

- [ ] 触发售后退款网关失败。
  - 请求：`POST /api/gw/orders/{orderId}/after-sales/{afterSaleId}/approve`
  - 期望：`502`
  - 期望错误码：`upstream_error`
  - 期望 details 字段包含：`errorCode`、`retryable`、`gateway`

### 4.3 支付回调失败分支

- [ ] 回调 `failed` / `cancelled`。
  - 请求：`POST /api/gw/orders/payments/callback`
  - 期望：`200`（accepted）
  - 二次校验：
    - 订单状态变为 `Failed`
    - 预占库存释放
    - 发布 `order.payment.failed` 与 `order.cancelled`

---

## 5. 物流轨迹契约

- [ ] 发货后轨迹包含 `Shipped` 事件（含 `CreatedAt/Status/Message/Source`）。
- [ ] 查询轨迹接口返回 provider/source 元信息。
  - 请求：`GET /api/gw/orders/{orderId}/sub-orders/{subOrderId}/tracking`
  - 期望：`200`
  - 期望结构：`data.provider`、`data.source`、`data.events[]`

- [ ] 重复查询不应无限累积重复轨迹（去重生效）。
  - 连续调用两次 tracking 查询
  - 期望：事件数量稳定或仅有合理新增（不出现同一事件重复堆积）

---

## 6. 定时上下架幂等契约

- [ ] 对同一商品设置到期 schedule，连续执行两次：
  - 请求：`POST /api/gw/catalog/ops/apply-shelf-schedules?take=...`
  - 第一次：可能 `applied > 0`
  - 第二次：期望不重复变更（通常 `applied = 0`）

- [ ] Job 入口与 ops 入口行为一致。
  - `POST /api/gw/jobs/run/apply-catalog-shelf-schedules?take=...`
  - 期望：Job run 记录可见，结果摘要与 ops 行为一致

---

## 7. 最小验收结论模板

- [ ] P0 核心契约全部通过（类目治理、权限收口、下单规则）。
- [ ] P1 骨架契约全部通过（支付失败分层、物流 provider/source、去重、任务幂等）。
- [ ] 回归无阻断：关键接口无 5xx 非预期错误。

建议记录：

- 执行时间：
- 执行环境：
- 失败用例编号：
- 失败请求/响应摘要：
- 修复单号：
