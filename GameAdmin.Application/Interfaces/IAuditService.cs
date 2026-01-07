using GameAdmin.Application.DTOs;
using GameAdmin.Domain.Entities;

namespace GameAdmin.Application.Interfaces;

/// <summary>
/// 审批服务接口
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// 获取所有待审批的操作
    /// </summary>
    Task<IReadOnlyList<PendingOperationDto>> GetPendingOperationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 审批通过
    /// </summary>
    Task<AuditDecisionResponse?> ApproveAsync(Guid logId, Guid approverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 审批拒绝
    /// </summary>
    Task<AuditDecisionResponse?> RejectAsync(Guid logId, Guid approverId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式获取操作日志（用于导出）
    /// </summary>
    IAsyncEnumerable<GmOperationLog> StreamLogsAsync(DateTime? startDate, DateTime? endDate, string? operationType, CancellationToken cancellationToken = default);
}
