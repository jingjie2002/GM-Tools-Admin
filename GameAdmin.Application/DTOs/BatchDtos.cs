using System.ComponentModel.DataAnnotations;

namespace GameAdmin.Application.DTOs;

/// <summary>
/// 批量封禁请求
/// </summary>
public record BatchBanRequest(
    [Required] List<Guid> PlayerIds,
    [Required] string Reason,
    int DurationHours = 0  // 0 = 永久
);

/// <summary>
/// 批量操作结果
/// </summary>
public record BatchOperationResult(
    int SuccessCount,
    int FailedCount,
    List<FailedItem> Failures,
    string Message
);

/// <summary>
/// 失败项详情
/// </summary>
public record FailedItem(
    Guid PlayerId,
    string Reason
);

/// <summary>
/// 导出日志请求
/// </summary>
public record ExportLogsRequest(
    DateTime? StartDate,
    DateTime? EndDate,
    string? OperationType
);
