namespace GameAdmin.Domain.Entities;

/// <summary>
/// GM 操作日志实体 - 记录所有后台管理操作
/// </summary>
public class GmOperationLog
{
    /// <summary>
    /// 日志唯一标识
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// 操作者 ID (AdminUser)
    /// </summary>
    public required Guid OperatorId { get; init; }

    /// <summary>
    /// 目标玩家 ID
    /// </summary>
    public required Guid TargetPlayerId { get; init; }

    /// <summary>
    /// 操作类型 (GiveItem, Ban, Unban 等)
    /// </summary>
    public required string OperationType { get; init; }

    /// <summary>
    /// 操作详情 (JSON 格式)
    /// </summary>
    public required string Details { get; init; }

    /// <summary>
    /// 操作状态
    /// </summary>
    public required GmOperationStatus Status { get; set; }

    /// <summary>
    /// 审批人 ID
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    /// <summary>
    /// 审批时间
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// 创建新日志的工厂方法
    /// </summary>
    public static GmOperationLog Create(
        Guid operatorId,
        Guid targetPlayerId,
        string operationType,
        string details,
        GmOperationStatus status = GmOperationStatus.Success)
    {
        return new GmOperationLog
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            TargetPlayerId = targetPlayerId,
            OperationType = operationType,
            Details = details,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }
}

