using System.Security.Claims;
using GameAdmin.Application.DTOs;
using GameAdmin.Application.Interfaces;
using GameAdmin.Infrastructure.Services;
using GameAdmin.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameAdmin.Api.Controllers;

/// <summary>
/// 玩家管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PlayerController(
    IPlayerService playerService, 
    BanQueueService banQueueService,
    ILogger<PlayerController> logger) : ControllerBase
{
    /// <summary>
    /// 获取玩家列表（支持分页与搜索）
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PlayerDto>>> GetList(
        [FromQuery] PlayerQueryDto query,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(">>> [接口触发] 正在获取玩家列表... Keyword={Keyword}, Page={Page}, PageSize={PageSize}",
            query.Keyword ?? "(空)", query.Page, query.PageSize);

        var result = await playerService.GetPlayersAsync(query, cancellationToken);

        logger.LogInformation(">>> [数据诊断] 数据库中实际查到的玩家数量: {Count}, 总数: {TotalCount}",
            result.Items.Count, result.TotalCount);

        return Ok(result);
    }

    /// <summary>
    /// GM 道具发放
    /// </summary>
    [HttpPost("give-item")]
    public async Task<ActionResult<GiveItemResponse>> GiveItem(
        GiveItemRequest request,
        CancellationToken cancellationToken)
    {
        var operatorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(operatorIdClaim) || !Guid.TryParse(operatorIdClaim, out var operatorId))
        {
            return Unauthorized(new { message = "无效的操作者身份" });
        }

        var result = await playerService.GiveItemAsync(request, operatorId, cancellationToken);

        if (result is null)
        {
            return NotFound(new { message = "玩家不存在" });
        }

        return Ok(result);
    }

    /// <summary>
    /// 扣除玩家金币 (需二次验证)
    /// </summary>
    [HttpPost("deduct-gold")]
    [RequireSecondaryAuth]
    public async Task<ActionResult<GiveItemResponse>> DeductGold(
        DeductGoldRequest request,
        CancellationToken cancellationToken)
    {
        var operatorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(operatorIdClaim) || !Guid.TryParse(operatorIdClaim, out var operatorId))
        {
            return Unauthorized(new { message = "无效的操作者身份" });
        }

        try
        {
            var result = await playerService.DeductGoldAsync(request, operatorId, cancellationToken);

            if (result is null)
            {
                return NotFound(new { message = "玩家不存在" });
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // 余额不足等业务异常
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 解封玩家
    /// </summary>
    [HttpPost("unban-player")]
    public async Task<ActionResult> UnbanPlayer(
        UnbanPlayerRequest request,
        CancellationToken cancellationToken)
    {
        var operatorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(operatorIdClaim) || !Guid.TryParse(operatorIdClaim, out var operatorId))
        {
            return Unauthorized(new { message = "无效的操作者身份" });
        }

        var success = await playerService.UnbanPlayerAsync(request, operatorId, cancellationToken);
        if (!success)
        {
            return NotFound(new { message = "玩家不存在" });
        }

        return Ok(new { success = true, message = "玩家已解封" });
    }

    /// <summary>
    /// 批量封禁玩家 (异步队列处理)
    /// </summary>
    [HttpPost("batch-ban")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> BatchBan(
        BatchBanRequest request)
    {
        var operatorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(operatorIdClaim) || !Guid.TryParse(operatorIdClaim, out var operatorId))
        {
            return Unauthorized(new { message = "无效的操作者身份" });
        }

        if (request.PlayerIds.Count == 0)
        {
            return BadRequest(new { message = "请选择要封禁的玩家" });
        }

        if (request.PlayerIds.Count > 500)
        {
            return BadRequest(new { message = "单次批量封禁不能超过 500 人" });
        }

        // 加入队列异步处理
        var batchId = await banQueueService.EnqueueBanAsync(request, operatorId);

        logger.LogInformation("Batch ban queued: BatchId={BatchId}, Count={Count}, Operator={OperatorId}",
            batchId, request.PlayerIds.Count, operatorId);

        return Accepted(new
        {
            message = $"批量封禁请求已提交，共 {request.PlayerIds.Count} 人",
            batchId,
            estimatedSeconds = request.PlayerIds.Count / 50 + 1  // 50/s 处理速度
        });
    }
}
