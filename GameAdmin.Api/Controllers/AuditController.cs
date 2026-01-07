using System.Security.Claims;
using GameAdmin.Application.DTOs;
using GameAdmin.Application.Interfaces;
using MiniExcelLibs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameAdmin.Api.Controllers;

/// <summary>
/// 审批管理控制器
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/[controller]")]
public class AuditController(IAuditService auditService) : ControllerBase
{
    /// <summary>
    /// 获取待审批列表
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PendingOperationDto>>> GetPending(
        CancellationToken cancellationToken)
    {
        var result = await auditService.GetPendingOperationsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 审批决策 (Approve/Reject)
    /// </summary>
    [HttpPost("decide")]
    public async Task<ActionResult<AuditDecisionResponse>> Decide(
        AuditDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var approverIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(approverIdClaim) || !Guid.TryParse(approverIdClaim, out var approverId))
        {
            return Unauthorized(new { message = "无效的审批者身份" });
        }

        AuditDecisionResponse? result;

        switch (request.Action.ToUpperInvariant())
        {
            case "APPROVE":
                result = await auditService.ApproveAsync(request.LogId, approverId, cancellationToken);
                break;

            case "REJECT":
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    return BadRequest(new { message = "拒绝操作必须提供原因" });
                }
                result = await auditService.RejectAsync(request.LogId, approverId, request.Reason, cancellationToken);
                break;

            default:
                return BadRequest(new { message = "无效的操作类型，请使用 Approve 或 Reject" });
        }

        if (result is null)
        {
            return NotFound(new { message = "待审批记录不存在或已被处理" });
        }

        return Ok(result);
    }

    /// <summary>
    /// 导出操作日志为 Excel (流式)
    /// </summary>
    [HttpGet("export")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? operationType,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"gm_operation_logs_{timestamp}.xlsx";

        // 使用 MemoryStream 收集数据
        using var stream = new MemoryStream();
        var exportData = await CollectLogsForExport(startDate, endDate, operationType, cancellationToken);
        await MiniExcel.SaveAsAsync(stream, exportData);
        stream.Position = 0;

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }

    private async Task<List<dynamic>> CollectLogsForExport(
        DateTime? startDate,
        DateTime? endDate,
        string? operationType,
        CancellationToken cancellationToken)
    {
        var result = new List<dynamic>();
        await foreach (var log in auditService.StreamLogsAsync(startDate, endDate, operationType, cancellationToken))
        {
            result.Add(new
            {
                日志ID = log.Id.ToString(),
                操作者ID = log.OperatorId.ToString(),
                目标玩家ID = log.TargetPlayerId.ToString(),
                操作类型 = log.OperationType,
                详情 = log.Details,
                状态 = log.Status.ToString(),
                创建时间 = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                审批人ID = log.ApprovedBy?.ToString() ?? "",
                审批时间 = log.ApprovedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
            });
        }
        return result;
    }
}


