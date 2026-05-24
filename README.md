# GM-Tools-Admin

GM-Tools-Admin 是一个基于 ASP.NET Core 和 Vue 3 的游戏后台管理系统示例。项目包含玩家管理、管理员登录、封禁操作、审计日志、SignalR 状态推送和前端管理页面。

后端采用分层结构组织 API、应用接口、领域模型和基础设施实现；前端使用 Vue 3、Vite 和 Element Plus 构建管理界面。

## 功能

- 管理员登录与 JWT 鉴权。
- 玩家列表、详情和分页查询。
- 玩家金币、道具和封禁相关操作。
- 二次确认过滤器，用于敏感接口校验。
- 操作日志记录与查询。
- SignalR 推送 GM 操作状态。
- PostgreSQL 数据持久化。
- Redis 服务封装。
- 前端登录页、仪表盘、玩家列表和审计列表。

## 技术栈

后端：

- .NET 10
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- Redis
- SignalR
- FluentValidation
- Serilog
- MiniExcel

前端：

- Vue 3
- TypeScript
- Vite
- Vue Router
- Element Plus
- Axios
- SignalR Client

## 目录结构

```text
GameAdmin.Api/             HTTP API、Hub、中间件和过滤器
GameAdmin.Application/     DTO 和服务接口
GameAdmin.Domain/          领域实体
GameAdmin.Infrastructure/  EF Core、Redis 和业务服务实现
game-admin-ui/             Vue 前端工程
docker-compose.yml         PostgreSQL、Redis 和 Seq 本地依赖
GameAdmin.sln              .NET 解决方案
```

## 本地运行

### 1. 启动依赖

```powershell
docker compose up -d db cache seq
```

默认依赖端口：

| 服务 | 地址 |
|---|---|
| PostgreSQL | `localhost:15432` |
| Redis | `localhost:6379` |
| Seq | `http://localhost:15341` |

### 2. 启动后端

```powershell
dotnet restore GameAdmin.sln
dotnet ef database update --project GameAdmin.Infrastructure --startup-project GameAdmin.Api
dotnet run --project GameAdmin.Api
```

后端配置位于：

```text
GameAdmin.Api/appsettings.json
GameAdmin.Api/appsettings.Development.json
```

### 3. 启动前端

```powershell
cd game-admin-ui
npm install
npm run dev
```

前端开发服务地址以 Vite 输出为准。

## 后端模块

| 模块 | 说明 |
|---|---|
| `GameAdmin.Api` | Controller、SignalR Hub、Filter 和中间件 |
| `GameAdmin.Application` | DTO、分页结果和服务接口 |
| `GameAdmin.Domain` | 玩家、管理员、权限和操作日志实体 |
| `GameAdmin.Infrastructure` | EF Core DbContext、迁移、Redis 和业务服务 |

## 前端模块

| 目录 | 说明 |
|---|---|
| `src/views` | 页面组件 |
| `src/api` | HTTP 请求封装 |
| `src/router` | 路由配置 |
| `src/utils` | 请求与 SignalR 工具 |

## 验证

后端构建：

```powershell
dotnet build GameAdmin.sln
```

前端构建：

```powershell
cd game-admin-ui
npm run build
```

## 当前限制

- 默认配置面向本地开发，生产环境需要修改 JWT 密钥、数据库密码和 CORS 配置。
- `appsettings.json` 中的默认账号和连接信息不应直接用于生产环境。
- 当前仓库没有提供完整自动化测试说明。
- 部署到服务器前需要补充环境变量、日志、数据库迁移和静态资源发布流程。

## License

MIT
