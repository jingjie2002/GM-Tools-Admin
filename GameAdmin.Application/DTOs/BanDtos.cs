using System.ComponentModel.DataAnnotations;

namespace GameAdmin.Application.DTOs;

/// <summary>
/// 封禁玩家请求
/// </summary>
public record BanPlayerRequest(
    [Required] Guid PlayerId,
    [Required] string Reason,
    [Range(0, 87600)] int DurationHours // 0表示永久，最长10年
);

/// <summary>
/// 封禁操作响应
/// </summary>
public record BanPlayerResponse(
    bool Success,
    string Message
);
