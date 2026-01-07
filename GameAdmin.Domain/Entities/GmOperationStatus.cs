namespace GameAdmin.Domain.Entities;

/// <summary>
/// GM 操作状态枚举
/// </summary>
public enum GmOperationStatus
{
    /// <summary>
    /// 已完成
    /// </summary>
    Success = 0,

    /// <summary>
    /// 待审批
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 已拒绝
    /// </summary>
    Rejected = 2
}
