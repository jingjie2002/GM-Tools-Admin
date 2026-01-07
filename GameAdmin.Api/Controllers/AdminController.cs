using System.Security.Claims;
using GameAdmin.Application.DTOs;
using GameAdmin.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameAdmin.Api.Controllers;

/// <summary>
/// 管理员控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AdminController(IPlayerService playerService, ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>
    /// 审批大额道具发放 (仅限超级管理员)
    /// </summary>
    [HttpPost("approve-item")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<ApproveItemResponse>> ApproveItem(
        ApproveItemRequest request,
        CancellationToken cancellationToken)
    {
        var approverIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(approverIdClaim) || !Guid.TryParse(approverIdClaim, out var approverId))
        {
            return Unauthorized(new { message = "无效的审批者身份" });
        }

        var result = await playerService.ApproveItemAsync(request.LogId, approverId, cancellationToken);

        if (result is null)
        {
            return NotFound(new { message = "待审批记录不存在或已处理" });
        }

        return Ok(result);
    }

    /// <summary>
    /// 拒绝大额道具发放 (仅限超级管理员)
    /// </summary>
    [HttpPost("reject-item")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<RejectItemResponse>> RejectItem(
        RejectItemRequest request,
        CancellationToken cancellationToken)
    {
        var rejecterIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(rejecterIdClaim) || !Guid.TryParse(rejecterIdClaim, out var rejecterId))
        {
            return Unauthorized(new { message = "无效的审批者身份" });
        }

        var result = await playerService.RejectItemAsync(request.LogId, request.Reason, rejecterId, cancellationToken);

        if (result is null)
        {
            return NotFound(new { message = "待审批记录不存在或已处理" });
        }

        return Ok(result);
    }

    /// <summary>
    /// 获取每日运营统计 (仅限超级管理员)
    /// </summary>
    [HttpGet("stats/daily")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<GmStatsDto>> GetDailyStats(CancellationToken cancellationToken)
    {
        var stats = await playerService.GetDailyStatsAsync(cancellationToken);
        return Ok(stats);
    }


    /// <summary>
    /// 封禁玩家 (支持 Admin 和 SuperAdmin)
    /// </summary>
    [HttpPost("ban-player")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<ActionResult<BanPlayerResponse>> BanPlayer(
        BanPlayerRequest request,
        CancellationToken cancellationToken)
    {
        var operatorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(operatorIdClaim) || !Guid.TryParse(operatorIdClaim, out var operatorId))
        {
            return Unauthorized(new { message = "无效的操作者身份" });
        }

        var result = await playerService.BanPlayerAsync(request, operatorId, cancellationToken);

        if (result is null)
        {
            return NotFound(new { message = "玩家不存在" });
        }

        // 审计日志
        logger.LogWarning("GM: Player {PlayerId} banned for {Reason}", request.PlayerId, request.Reason);

        return Ok(result);
    }
}
