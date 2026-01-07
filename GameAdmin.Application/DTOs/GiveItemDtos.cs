using System.ComponentModel.DataAnnotations;

namespace GameAdmin.Application.DTOs;

/// <summary>
/// GM 道具发放请求
/// </summary>
public record GiveItemRequest(
    Guid PlayerId,

    [Required(ErrorMessage = "道具类型不能为空")]
    string ItemType,

    [Range(1, long.MaxValue, ErrorMessage = "金额必须大于0")]
    long Amount
);

/// <summary>
/// GM 道具扣除请求
/// </summary>
public record DeductGoldRequest(
    Guid PlayerId,

    [Range(1, long.MaxValue, ErrorMessage = "扣除金额必须大于0")]
    long Amount,
    
    [Required(ErrorMessage = "扣除原因不能为空")]
    string Reason
);

/// <summary>
/// GM 道具发放/扣除响应
/// </summary>
public record GiveItemResponse(
    Guid PlayerId,
    string ItemType,
    long Amount,
    long NewBalance,
    DateTime OperatedAt,
    string Status,
    string? Message = null
);

/// <summary>
/// 解封请求
/// </summary>
public record UnbanPlayerRequest(
    Guid PlayerId,
    [Required] string Reason
);

/// <summary>
/// 审批请求
/// </summary>
public record ApproveItemRequest(Guid LogId);

/// <summary>
/// 拒绝请求
/// </summary>
public record RejectItemRequest(
    Guid LogId,

    [Required(ErrorMessage = "拒绝原因不能为空")]
    string Reason
);

/// <summary>
/// 审批/拒绝响应
/// </summary>
public record ApproveItemResponse(
    Guid LogId,
    Guid PlayerId,
    string Status,
    long NewBalance,
    DateTime ApprovedAt
);

/// <summary>
/// 拒绝响应
/// </summary>
public record RejectItemResponse(
    Guid LogId,
    Guid PlayerId,
    string Status,
    string Reason,
    DateTime RejectedAt
);
