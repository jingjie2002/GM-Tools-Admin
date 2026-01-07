# 项目开发计划 (Updated)

## 阶段一：核心框架搭建 (已完成)
- [x] 创建 ASP.NET Core Web API 项目
- [x] 配置 PostgreSQL 数据库连接
- [x] 集成 JWT 认证系统
- [x] 实现基础的 Admin 用户管理模块 (Seed Data)
- [x] 实现 GM 操作日志的基础记录功能

## 阶段二：GM 基础功能 (已完成)
- [x] 玩家信息查询接口
- [x] 道具发放功能 (Give Item)
- [x] 道具发放审批流 (超过 5000 金币需审批)
- [x] 审批拒绝工作流
- [x] 每日 GM 操作统计接口

## 阶段三：高级查询与监控 (进行中)
- [x] **玩家查询系统 (已完成)**: 分页、模糊搜索、多条件筛选
- [x] 安全初始化 (DbInitializer): 仅开发环境下的安全数据播种
- [x] 系统运行监控: 集成 Seq 日志平台 (已配置 docker)
- [x] 慢查询优化与数据库索引调优

## 阶段四：安全与审计 (计划中)
- [ ] 操作日志导出 (CSV/Excel)
- [ ] 敏感操作二次确认 (TOTP)
- [ ] IP 白名单机制

## 阶段五：高并发与工程化增强 (2026 进阶版)
- [x] Redis 基础集成 (Caching)
- [x] 分布式锁 (Distributed Lock)
- [x] Token 黑名单机制 (Security)
- [x] 玩家封禁与强制踢人 (Redis Kick)
- [x] FluentValidation 统一参数校验
- [x] 统一异常处理规范 (Global Exception Filter)
- [x] Serilog 结构化日志
- [ ] 后台异步任务队列 (Hangfire/Quartz)
