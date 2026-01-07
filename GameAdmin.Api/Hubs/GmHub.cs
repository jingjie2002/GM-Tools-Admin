using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GameAdmin.Api.Hubs;

/// <summary>
/// GM 管理系统实时推送 Hub
/// </summary>
[Authorize]
public class GmHub : Hub
{
    private readonly ILogger<GmHub> _logger;

    public GmHub(ILogger<GmHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 客户端连接时自动加入管理员组
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var username = Context.User?.Identity?.Name ?? "Unknown";

        _logger.LogInformation("SignalR client connected: {ConnectionId}, User: {Username}", 
            Context.ConnectionId, username);

        // 将管理员加入 AdminGroup
        await Groups.AddToGroupAsync(Context.ConnectionId, "AdminGroup");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开连接
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AdminGroup");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 广播统计数据更新（供服务端调用）
    /// </summary>
    public static async Task BroadcastStatsUpdateAsync(IHubContext<GmHub> hubContext)
    {
        await hubContext.Clients.Group("AdminGroup").SendAsync("StatsUpdated");
    }

    /// <summary>
    /// 广播新的待审批通知
    /// </summary>
    public static async Task BroadcastNewPendingAsync(IHubContext<GmHub> hubContext, Guid logId, string operatorName, long amount)
    {
        await hubContext.Clients.Group("AdminGroup").SendAsync("NewPendingAudit", new
        {
            LogId = logId,
            OperatorName = operatorName,
            Amount = amount,
            Timestamp = DateTime.UtcNow
        });
    }
}
