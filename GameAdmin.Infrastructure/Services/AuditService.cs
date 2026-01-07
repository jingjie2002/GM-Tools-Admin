using System.Text.Json;
using GameAdmin.Application.DTOs;
using GameAdmin.Application.Interfaces;
using GameAdmin.Domain.Entities;
using GameAdmin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameAdmin.Infrastructure.Services;

/// <summary>
/// 审批服务实现
/// </summary>
public class AuditService(
    AppDbContext dbContext,
    ILogger<AuditService> logger) : IAuditService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingOperationDto>> GetPendingOperationsAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching pending operations");

        var pendingLogs = await dbContext.GmOperationLogs
            .AsNoTracking()
            .Where(l => l.Status == GmOperationStatus.Pending)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<PendingOperationDto>();

        foreach (var log in pendingLogs)
        {
            // 获取操作者信息
            var operatorUser = await dbContext.AdminUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == log.OperatorId, cancellationToken);

            // 获取玩家信息
            var player = await dbContext.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == log.TargetPlayerId, cancellationToken);

            // 解析详情获取 ItemType 和 Amount
            var details = JsonSerializer.Deserialize<JsonElement>(log.Details);
            var itemType = details.TryGetProperty("ItemType", out var it) ? it.GetString() ?? "Unknown" : "Unknown";
            var amount = details.TryGetProperty("Amount", out var am) ? am.GetInt64() : 0;

            result.Add(new PendingOperationDto(
                LogId: log.Id,
                OperatorId: log.OperatorId,
                OperatorName: operatorUser?.Username ?? "Unknown",
                PlayerId: log.TargetPlayerId,
                PlayerNickname: player?.Nickname ?? "Unknown",
                ItemType: itemType,
                Amount: amount,
                CreatedAt: log.CreatedAt
            ));
        }

        logger.LogInformation("Found {Count} pending operations", result.Count);
        return result;
    }

    /// <inheritdoc />
    public async Task<AuditDecisionResponse?> ApproveAsync(
        Guid logId,
        Guid approverId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Approving operation {LogId} by {ApproverId}", logId, approverId);

        // 使用 ExecutionStrategy 包裹整个事务以兼容 EnableRetryOnFailure
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // 查找待审批记录
            var log = await dbContext.GmOperationLogs
                .FirstOrDefaultAsync(l => l.Id == logId && l.Status == GmOperationStatus.Pending, cancellationToken);

            if (log is null)
            {
                logger.LogWarning("Pending operation not found: {LogId}", logId);
                return null;
            }

            // 查找目标玩家
            var player = await dbContext.Players
                .FirstOrDefaultAsync(p => p.Id == log.TargetPlayerId, cancellationToken);

            if (player is null)
            {
                logger.LogWarning("Player not found: {PlayerId}", log.TargetPlayerId);
                return null;
            }

            // 解析详情获取 Amount
            var details = JsonSerializer.Deserialize<JsonElement>(log.Details);
            var amount = details.TryGetProperty("Amount", out var am) ? am.GetInt64() : 0;

            // 在 ExecutionStrategy 内部使用事务
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. 增加玩家金币
                player.Gold += amount;

                // 2. 更新日志状态
                log.Status = GmOperationStatus.Success;
                log.ApprovedBy = approverId;
                log.ApprovedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation(
                    "Operation {LogId} approved. Player {PlayerId} balance updated to {NewBalance}",
                    logId, player.Id, player.Gold);

                return new AuditDecisionResponse(
                    LogId: logId,
                    PlayerId: player.Id,
                    Status: "Approved",
                    Message: $"已批准，玩家余额增加 {amount}",
                    NewBalance: player.Gold,
                    ProcessedAt: DateTime.UtcNow
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogError(ex, "Failed to approve operation {LogId}", logId);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<AuditDecisionResponse?> RejectAsync(
        Guid logId,
        Guid approverId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Rejecting operation {LogId} by {ApproverId}, Reason: {Reason}", logId, approverId, reason);

        // 查找待审批记录
        var log = await dbContext.GmOperationLogs
            .FirstOrDefaultAsync(l => l.Id == logId && l.Status == GmOperationStatus.Pending, cancellationToken);

        if (log is null)
        {
            logger.LogWarning("Pending operation not found: {LogId}", logId);
            return null;
        }

        // 查找目标玩家 (仅用于返回信息)
        var player = await dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == log.TargetPlayerId, cancellationToken);

        // 更新日志状态为拒绝 (不触发金币增加)
        log.Status = GmOperationStatus.Rejected;
        log.ApprovedBy = approverId;
        log.ApprovedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Operation {LogId} rejected", logId);

        return new AuditDecisionResponse(
            LogId: logId,
            PlayerId: log.TargetPlayerId,
            Status: "Rejected",
            Message: $"已拒绝: {reason}",
            NewBalance: player?.Gold,
            ProcessedAt: DateTime.UtcNow
        );
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<GmOperationLog> StreamLogsAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? operationType,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Streaming logs: StartDate={StartDate}, EndDate={EndDate}, Type={Type}",
            startDate, endDate, operationType);

        var query = dbContext.GmOperationLogs.AsNoTracking();

        if (startDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(operationType))
        {
            query = query.Where(l => l.OperationType == operationType);
        }

        query = query.OrderByDescending(l => l.CreatedAt);

        await foreach (var log in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return log;
        }
    }
}
