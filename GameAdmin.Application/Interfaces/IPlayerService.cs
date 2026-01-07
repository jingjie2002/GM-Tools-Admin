using GameAdmin.Application.DTOs;

namespace GameAdmin.Application.Interfaces;

/// <summary>
/// 玩家服务接口
/// </summary>
public interface IPlayerService
{
    /// <summary>
    /// 获取玩家列表（分页支持）
    /// </summary>
    Task<PagedResult<PlayerDto>> GetPlayersAsync(
        PlayerQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GM 道具发放
    /// </summary>
    Task<GiveItemResponse?> GiveItemAsync(
        GiveItemRequest request,
        Guid operatorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 审批大额发放
    /// </summary>
    Task<ApproveItemResponse?> ApproveItemAsync(
        Guid logId,
        Guid approverId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 拒绝大额发放
    /// </summary>
    Task<RejectItemResponse?> RejectItemAsync(
        Guid logId,
        string reason,
        Guid rejecterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取每日统计数据
    /// </summary>
    Task<GmStatsDto> GetDailyStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 扣除金币
    /// </summary>
    Task<GiveItemResponse?> DeductGoldAsync(
        DeductGoldRequest request,
        Guid operatorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 封禁玩家并强制下线
    /// </summary>
    Task<BanPlayerResponse?> BanPlayerAsync(
        BanPlayerRequest request,
        Guid operatorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 解封玩家
    /// </summary>
    Task<bool> UnbanPlayerAsync(
        UnbanPlayerRequest request,
        Guid operatorId,
        CancellationToken cancellationToken = default);
}
