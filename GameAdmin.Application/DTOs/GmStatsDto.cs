namespace GameAdmin.Application.DTOs;

/// <summary>
/// GM 统计数据 DTO
/// </summary>
public record GmStatsDto(
    /// <summary>
    /// 当前在线玩家数
    /// </summary>
    int OnlineCount,

    /// <summary>
    /// 过去24小时内发放的总金币数
    /// </summary>
    long TotalGoldIssued,

    /// <summary>
    /// 待审批数量
    /// </summary>
    int PendingCount,

    /// <summary>
    /// 封禁玩家总数
    /// </summary>
    int BannedCount,

    /// <summary>
    /// 发奖最多的管理员列表
    /// </summary>
    IReadOnlyList<TopAdminDto> TopAdmins
);

/// <summary>
/// 管理员发奖排行
/// </summary>
public record TopAdminDto(
    /// <summary>
    /// 管理员 ID
    /// </summary>
    Guid AdminId,

    /// <summary>
    /// 管理员用户名
    /// </summary>
    string AdminName,

    /// <summary>
    /// 发奖总额
    /// </summary>
    long TotalAmount,

    /// <summary>
    /// 操作次数
    /// </summary>
    int OperationCount
);
