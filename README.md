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

### 3) 访问地址

- OrderService: [http://localhost:5001](http://localhost:5001)
- ProductService: [http://localhost:5002](http://localhost:5002)
- ProductService Canary: [http://localhost:5003](http://localhost:5003)
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
2. 创建订单（触发状态写入、服务调用、事件发布）：

```bash
curl -X POST http://localhost:5001/api/orders -H "Content-Type: application/json" -d "{\"productId\":\"p-100\",\"quantity\":2}"
```

3. 查看事件处理结果：

```bash
curl http://localhost:5001/demo/events
```

4. 查看配置快照：

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

## 健康检查与接口

- 健康检查：`/health/live`、`/health/ready`
- 死信查询（示例）：`GET /ops/outbox/deadletters?take=100`
- 死信重放（示例）：`POST /ops/outbox/deadletters/replay-all?dryRun=true`
