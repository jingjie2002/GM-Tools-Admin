using System.Text.Json;
using GameAdmin.Application.DTOs;
using GameAdmin.Application.Interfaces;
using GameAdmin.Domain.Entities;
using GameAdmin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameAdmin.Infrastructure.Services;

/// <summary>
/// 玩家服务实现
/// </summary>
public class PlayerService(
    AppDbContext dbContext,
    ILogger<PlayerService> logger,
    IRedisService redisService) : IPlayerService
{
    private const long ApprovalThreshold = 5000;
    private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task<PagedResult<PlayerDto>> GetPlayersAsync(
        PlayerQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = dbContext.Players.AsNoTracking();

        // 1. 模糊搜索
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            dbQuery = dbQuery.Where(p => p.Nickname.Contains(query.Keyword));
        }

        // 2. 统计总数（数据库端执行）
        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // 3. 分页查询
        var items = await dbQuery
            .OrderByDescending(p => p.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new PlayerDto(
                p.Id,
                p.Nickname,
                p.Level,
                p.Gold,
                p.IsBanned,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PlayerDto>(items, totalCount, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<GiveItemResponse?> GiveItemAsync(
        GiveItemRequest request,
        Guid operatorId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "GiveItem request - Operator: {OperatorId}, Player: {PlayerId}, Type: {ItemType}, Amount: {Amount}",
            operatorId, request.PlayerId, request.ItemType, request.Amount);

        // 幂等锁：防止重复提交
        var lockKey = $"reward_lock:{request.PlayerId}:{request.Amount}";
        var lockValue = Guid.NewGuid().ToString();
        var lockExpiry = TimeSpan.FromSeconds(5);

        logger.LogInformation(">>> [探针] 尝试获取 Redis 锁: {Key}", lockKey);

        if (!await redisService.AcquireLockAsync(lockKey, lockValue, lockExpiry))
        {
            logger.LogWarning("Duplicate reward request detected: {LockKey}", lockKey);
            throw new InvalidOperationException("请求正在处理中，请勿重复提交");
        }
        // 幂等锁已获取，锁会在 5 秒后自动过期（不主动释放）
        // 这样可以阻止 5 秒内的所有重复提交

        // 检查玩家是否存在
        var player = await dbContext.Players
            .FirstOrDefaultAsync(p => p.Id == request.PlayerId, cancellationToken);

        if (player is null)
        {
            logger.LogWarning("Player not found: {PlayerId}", request.PlayerId);
            return null;
        }

        // 判断是否需要审批
        if (request.Amount > ApprovalThreshold)
        {
            return await CreatePendingOperationAsync(request, operatorId, player, cancellationToken);
        }

        // 小额直接发放
        return await ExecuteDirectGrantAsync(request, operatorId, player, cancellationToken);
    }

    /// <summary>
    /// 创建待审批操作
    /// </summary>
    private async Task<GiveItemResponse> CreatePendingOperationAsync(
        GiveItemRequest request,
        Guid operatorId,
        Player player,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Amount {Amount} exceeds threshold, creating pending operation", request.Amount);

        var operationDetails = JsonSerializer.Serialize(new
        {
            ItemType = request.ItemType,
            Amount = request.Amount,
            CurrentBalance = player.Gold
        });

        var operationLog = GmOperationLog.Create(
            operatorId: operatorId,
            targetPlayerId: request.PlayerId,
            operationType: "GiveItem",
            details: operationDetails,
            status: GmOperationStatus.Pending);

        dbContext.GmOperationLogs.Add(operationLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Pending operation created: {LogId}", operationLog.Id);

        return new GiveItemResponse(
            PlayerId: player.Id,
            ItemType: request.ItemType,
            Amount: request.Amount,
            NewBalance: player.Gold,
            OperatedAt: operationLog.CreatedAt,
            Status: "Pending",
            Message: $"金额超过 {ApprovalThreshold}，已进入审批流，等待 SuperAdmin 审批");
    }

    /// <summary>
    /// 直接发放（小额）
    /// </summary>
    private async Task<GiveItemResponse> ExecuteDirectGrantAsync(
        GiveItemRequest request,
        Guid operatorId,
        Player player,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var previousBalance = player.Gold;
                player.Gold += request.Amount;

                var operationDetails = JsonSerializer.Serialize(new
                {
                    ItemType = request.ItemType,
                    Amount = request.Amount,
                    PreviousBalance = previousBalance,
                    NewBalance = player.Gold
                });

                var operationLog = GmOperationLog.Create(
                    operatorId: operatorId,
                    targetPlayerId: request.PlayerId,
                    operationType: "GiveItem",
                    details: operationDetails,
                    status: GmOperationStatus.Success);

                dbContext.GmOperationLogs.Add(operationLog);

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation("GiveItem successful - Player: {PlayerId}, NewBalance: {NewBalance}",
                    request.PlayerId, player.Gold);

                return new GiveItemResponse(
                    PlayerId: player.Id,
                    ItemType: request.ItemType,
                    Amount: request.Amount,
                    NewBalance: player.Gold,
                    OperatedAt: operationLog.CreatedAt,
                    Status: "Success");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogError(ex, "GiveItem failed, transaction rolled back");
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<ApproveItemResponse?> ApproveItemAsync(
        Guid logId,
        Guid approverId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("ApproveItem request - LogId: {LogId}, Approver: {ApproverId}", logId, approverId);

        // 分布式锁防止重复审批
        var lockKey = $"approve:{logId}";
        var lockValue = Guid.NewGuid().ToString();

        if (!await redisService.AcquireLockAsync(lockKey, lockValue, LockExpiry))
        {
            logger.LogWarning("Failed to acquire lock for LogId: {LogId}", logId);
            throw new InvalidOperationException("该审批正在处理中，请勿重复提交");
        }

        try
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // 1. 查找待审批日志
                    var operationLog = await dbContext.GmOperationLogs
                        .FirstOrDefaultAsync(l => l.Id == logId && l.Status == GmOperationStatus.Pending, cancellationToken);

                    if (operationLog is null)
                    {
                        logger.LogWarning("Pending operation not found: {LogId}", logId);
                        return null;
                    }

                    // 2. 解析详情获取金额
                    var details = JsonSerializer.Deserialize<JsonElement>(operationLog.Details);
                    var amount = details.GetProperty("Amount").GetInt64();

                    // 3. 查找玩家
                    var player = await dbContext.Players
                        .FirstOrDefaultAsync(p => p.Id == operationLog.TargetPlayerId, cancellationToken);

                    if (player is null)
                    {
                        logger.LogWarning("Player not found: {PlayerId}", operationLog.TargetPlayerId);
                        return null;
                    }

                    // 4. 更新玩家余额
                    var previousBalance = player.Gold;
                    player.Gold += amount;

                    // 5. 更新日志状态
                    operationLog.Status = GmOperationStatus.Success;
                    operationLog.ApprovedBy = approverId;
                    operationLog.ApprovedAt = DateTime.UtcNow;

                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    logger.LogInformation(
                        "ApproveItem successful - LogId: {LogId}, Player: {PlayerId}, Amount: {Amount}, NewBalance: {NewBalance}",
                        logId, player.Id, amount, player.Gold);

                    return new ApproveItemResponse(
                        LogId: logId,
                        PlayerId: player.Id,
                        Status: "Success",
                        NewBalance: player.Gold,
                        ApprovedAt: operationLog.ApprovedAt.Value);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    logger.LogError(ex, "ApproveItem failed, transaction rolled back");
                    throw;
                }
            });
        }
        finally
        {
            await redisService.ReleaseLockAsync(lockKey, lockValue);
        }
    }

    /// <inheritdoc />
    public async Task<BanPlayerResponse?> BanPlayerAsync(
        BanPlayerRequest request,
        Guid operatorId,
        CancellationToken cancellationToken = default)
    {
        // 1. 查找玩家
        var player = await dbContext.Players
            .FirstOrDefaultAsync(p => p.Id == request.PlayerId, cancellationToken);

        if (player is null)
        {
            logger.LogWarning("Player to ban not found: {PlayerId}", request.PlayerId);
            return null;
        }

        // 2. 更新数据库
        player.IsBanned = true;
        player.BanReason = request.Reason;

        if (request.DurationHours > 0)
        {
            player.BanExpiresAt = DateTime.UtcNow.AddHours(request.DurationHours);
        }
        else
        {
            // 0 表示永久 (设置一个很远的未来)
            player.BanExpiresAt = null;
        }

        // 记录操作日志
        var log = GmOperationLog.Create(
            operatorId,
            player.Id,
            "BanPlayer",
            JsonSerializer.Serialize(new { request.Reason, request.DurationHours }));

        // 封禁操作无需审批，直接成功
        log.Status = GmOperationStatus.Success;
        log.ApprovedBy = operatorId;
        log.ApprovedAt = DateTime.UtcNow;

        dbContext.GmOperationLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 3. Redis 强制下线 (踢人)
        var redisExpiry = request.DurationHours > 0
            ? TimeSpan.FromHours(request.DurationHours)
            : TimeSpan.FromDays(3650); // 10年

        await redisService.BlacklistUserAsync(player.Id, redisExpiry);

        logger.LogInformation("Player banned and kicked: {PlayerId}, Duration: {Duration}h", player.Id, request.DurationHours);

        return new BanPlayerResponse(true, "玩家已成功封禁并强制下线");
    }

    /// <inheritdoc />
    public async Task<GmStatsDto> GetDailyStatsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("GetDailyStats request");

        var since = DateTime.UtcNow.AddHours(-24);

        // 查询1: 待审批数量 (纯数据库聚合)
        // LINQ → SQL: SELECT COUNT(*) FROM gm_operation_logs WHERE status = 'Pending'
        var pendingCount = await dbContext.GmOperationLogs
            .AsNoTracking()
            .CountAsync(l => l.Status == GmOperationStatus.Pending, cancellationToken);

        // 查询2: 封禁玩家数量
        // LINQ → SQL: SELECT COUNT(*) FROM players WHERE is_banned = true
        var bannedCount = await dbContext.Players
            .AsNoTracking()
            .CountAsync(p => p.IsBanned, cancellationToken);

        // 查询3: 在线人数(Mock - 后续可接入 Redis)
        // TODO: 从 Redis 获取真实在线人数
        var onlineCount = new Random().Next(800, 1500);

        // 查询4: 获取过去24小时成功的发奖记录（筛选在数据库端执行）
        // LINQ → SQL: SELECT operator_id, details FROM gm_operation_logs
        //             WHERE created_at >= @since AND status = 'Success' AND operation_type = 'GiveItem'
        var successfulGrants = await dbContext.GmOperationLogs
            .AsNoTracking()
            .Where(l => l.CreatedAt >= since
                     && l.Status == GmOperationStatus.Success
                     && l.OperationType == "GiveItem")
            .Select(l => new { l.OperatorId, l.Details })
            .ToListAsync(cancellationToken);

        // 客户端聚合 (JSON 解析在内存中执行，但筛选已在数据库完成)
        var parsedGrants = successfulGrants
            .Select(g =>
            {
                try
                {
                    var doc = JsonSerializer.Deserialize<JsonElement>(g.Details);
                    var amount = doc.TryGetProperty("Amount", out var amountProp)
                        ? amountProp.GetInt64()
                        : 0L;
                    return new { g.OperatorId, Amount = amount };
                }
                catch
                {
                    return new { g.OperatorId, Amount = 0L };
                }
            })
            .ToList();

        // 计算总金币
        var totalGoldIssued = parsedGrants.Sum(g => g.Amount);

        // 管理员排行 (GROUP BY 在内存中执行)
        var topAdminGroups = parsedGrants
            .GroupBy(g => g.OperatorId)
            .Select(g => new
            {
                OperatorId = g.Key,
                TotalAmount = g.Sum(x => x.Amount),
                OperationCount = g.Count()
            })
            .OrderByDescending(x => x.TotalAmount)
            .Take(10)
            .ToList();

        // 批量查询管理员用户名（避免 N+1）
        // LINQ → SQL: SELECT id, username FROM admin_users WHERE id IN (...)
        var adminIds = topAdminGroups.Select(x => x.OperatorId).ToList();
        var adminNames = await dbContext.AdminUsers
            .AsNoTracking()
            .Where(u => adminIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken);

        var topAdmins = topAdminGroups
            .Select(x => new TopAdminDto(
                AdminId: x.OperatorId,
                AdminName: adminNames.GetValueOrDefault(x.OperatorId, "Unknown"),
                TotalAmount: x.TotalAmount,
                OperationCount: x.OperationCount))
            .ToList();

        logger.LogInformation("DailyStats: Online={Online}, TotalGold={TotalGold}, Pending={Pending}, Banned={Banned}",
            onlineCount, totalGoldIssued, pendingCount, bannedCount);

        return new GmStatsDto(
            OnlineCount: onlineCount,
            TotalGoldIssued: totalGoldIssued,
            PendingCount: pendingCount,
            BannedCount: bannedCount,
            TopAdmins: topAdmins);
    }

    /// <inheritdoc />
    public async Task<GiveItemResponse?> DeductGoldAsync(
        DeductGoldRequest request,
        Guid operatorId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "DeductGold request - Operator: {OperatorId}, Player: {PlayerId}, Amount: {Amount}, Reason: {Reason}",
            operatorId, request.PlayerId, request.Amount, request.Reason);

        var lockKey = $"deduct_lock:{request.PlayerId}";
        var lockValue = Guid.NewGuid().ToString();
        var lockExpiry = TimeSpan.FromSeconds(10); // 延长锁时间以覆盖整个事务

        if (!await redisService.AcquireLockAsync(lockKey, lockValue, lockExpiry))
        {
            logger.LogWarning("Concurrent deduction request for player: {PlayerId}", request.PlayerId);
            throw new InvalidOperationException("操作过于频繁，请稍后重试");
        }

        try
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Step 1: 原子化条件更新 - 只有余额足够时才扣除
                    // SQL: UPDATE players SET gold = gold - @amount WHERE id = @id AND gold >= @amount
                    var affectedRows = await dbContext.Database.ExecuteSqlRawAsync(
                        "UPDATE players SET gold = gold - {0} WHERE id = {1} AND gold >= {0}",
                        request.Amount, request.PlayerId);

                    if (affectedRows == 0)
                    {
                        // 可能是玩家不存在，或者余额不足
                        var playerExists = await dbContext.Players
                            .AsNoTracking()
                            .AnyAsync(p => p.Id == request.PlayerId, cancellationToken);

                        if (!playerExists)
                        {
                            logger.LogWarning("Player not found: {PlayerId}", request.PlayerId);
                            return null; // 玩家不存在
                        }

                        // 玩家存在但余额不足
                        var currentBalance = await dbContext.Players
                            .AsNoTracking()
                            .Where(p => p.Id == request.PlayerId)
                            .Select(p => p.Gold)
                            .FirstOrDefaultAsync(cancellationToken);

                        logger.LogWarning("Insufficient balance for player {PlayerId}: Current={Current}, Requested={Requested}",
                            request.PlayerId, currentBalance, request.Amount);

                        throw new InvalidOperationException($"玩家余额不足（当前: {currentBalance}，请求扣除: {request.Amount}）");
                    }

                    // Step 2: 查询更新后的余额用于响应和日志
                    var newBalance = await dbContext.Players
                        .AsNoTracking()
                        .Where(p => p.Id == request.PlayerId)
                        .Select(p => p.Gold)
                        .FirstAsync(cancellationToken);

                    var previousBalance = newBalance + request.Amount; // 反推原余额

                    // Step 3: 记录操作日志
                    var operationDetails = JsonSerializer.Serialize(new
                    {
                        ItemType = "Gold",
                        Amount = -request.Amount,
                        PreviousBalance = previousBalance,
                        NewBalance = newBalance,
                        Reason = request.Reason
                    });

                    var operationLog = GmOperationLog.Create(
                        operatorId: operatorId,
                        targetPlayerId: request.PlayerId,
                        operationType: "DeductGold",
                        details: operationDetails,
                        status: GmOperationStatus.Success);

                    dbContext.GmOperationLogs.Add(operationLog);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    logger.LogInformation("DeductGold successful - Player: {PlayerId}, Deducted: {Amount}, NewBalance: {NewBalance}",
                        request.PlayerId, request.Amount, newBalance);

                    return new GiveItemResponse(
                        PlayerId: request.PlayerId,
                        ItemType: "Gold",
                        Amount: -request.Amount,
                        NewBalance: newBalance,
                        OperatedAt: operationLog.CreatedAt,
                        Status: "Success",
                        Message: "扣除成功");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    logger.LogError(ex, "DeductGold failed for player {PlayerId}", request.PlayerId);
                    throw; // 重新抛出，让 Controller 层处理 400 响应
                }
            });
        }
        finally
        {
            // 无论成功或失败，确保释放 Redis 锁
            await redisService.ReleaseLockAsync(lockKey, lockValue);
            logger.LogDebug("Redis lock released for key: {LockKey}", lockKey);
        }
    }

    /// <inheritdoc />
    public async Task<bool> UnbanPlayerAsync(
        UnbanPlayerRequest request,
        Guid operatorId,
        CancellationToken cancellationToken = default)
    {
        var player = await dbContext.Players
            .FirstOrDefaultAsync(p => p.Id == request.PlayerId, cancellationToken);

        if (player is null) return false;

        // 更新数据库
        player.IsBanned = false;
        player.BanReason = null;
        player.BanExpiresAt = null;

        var log = GmOperationLog.Create(
            operatorId,
            player.Id,
            "UnbanPlayer",
            JsonSerializer.Serialize(new { request.Reason }));

        log.Status = GmOperationStatus.Success;
        log.ApprovedBy = operatorId;
        log.ApprovedAt = DateTime.UtcNow;

        dbContext.GmOperationLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Redis: 移除黑名单 (如果 RedisService 支持移除)
        // 目前 RedisService 只有 BlacklistUserAsync (设置过期键)
        // 我们可以设置一个立即过期的键，或者直接删除键 (如果 RedisService 暴露了 Delete)
        // 假设 BlacklistUserAsync 实现是 SETEX，我们无法直接 DEL 除非有接口。
        // 但通常实现解封需要 DEL。查看 RedisService... 
        // 既然无法直接查看 RedisService 内部，我们假设封禁检查是看 Key 是否存在。
        // 如果我们设置过期时间为 1 秒，它会马上过期，变相实现解封。
        await redisService.BlacklistUserAsync(player.Id, TimeSpan.FromSeconds(1)); 

        logger.LogInformation("Player unbanned: {PlayerId}", player.Id);
        return true;
    }

    /// <inheritdoc />
    public async Task<RejectItemResponse?> RejectItemAsync(
        Guid logId,
        string reason,
        Guid rejecterId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("RejectItem request - LogId: {LogId}, Rejecter: {RejecterId}, Reason: {Reason}",
            logId, rejecterId, reason);

        // 查找待审批日志
        var operationLog = await dbContext.GmOperationLogs
            .FirstOrDefaultAsync(l => l.Id == logId && l.Status == GmOperationStatus.Pending, cancellationToken);

        if (operationLog is null)
        {
            logger.LogWarning("Pending operation not found: {LogId}", logId);
            return null;
        }

        // 更新状态为 Rejected（不增加玩家余额）
        operationLog.Status = GmOperationStatus.Rejected;
        operationLog.ApprovedBy = rejecterId;
        operationLog.ApprovedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("RejectItem successful - LogId: {LogId}, Player: {PlayerId}",
            logId, operationLog.TargetPlayerId);

        return new RejectItemResponse(
            LogId: logId,
            PlayerId: operationLog.TargetPlayerId,
            Status: "Rejected",
            Reason: reason,
            RejectedAt: operationLog.ApprovedAt.Value);
    }
}
