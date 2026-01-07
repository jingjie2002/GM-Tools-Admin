namespace GameAdmin.Application.DTOs;

/// <summary>
/// 待审批记录 DTO
/// </summary>
public record PendingOperationDto(
    Guid LogId,
    Guid OperatorId,
    string OperatorName,
    Guid PlayerId,
    string PlayerNickname,
    string ItemType,
    long Amount,
    DateTime CreatedAt
);

/// <summary>
/// 审批决策请求
/// </summary>
public record AuditDecisionRequest(
    Guid LogId,
    string Action,  // "Approve" or "Reject"
    string? Reason = null
);

/// <summary>
/// 审批决策响应
/// </summary>
public record AuditDecisionResponse(
    Guid LogId,
    Guid PlayerId,
    string Status,
    string Message,
    long? NewBalance = null,
    DateTime? ProcessedAt = null
);
