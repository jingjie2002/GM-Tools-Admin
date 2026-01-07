using GameAdmin.Api.Hubs;
using GameAdmin.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;

namespace GameAdmin.Api.Services;

/// <summary>
/// 桥接 BanQueueService 事件到 SignalR Hub
/// </summary>
public class SignalRBroadcastService : IHostedService
{
    private readonly BanQueueService _banQueueService;
    private readonly IHubContext<GmHub> _hubContext;
    private readonly ILogger<SignalRBroadcastService> _logger;

    public SignalRBroadcastService(
        BanQueueService banQueueService,
        IHubContext<GmHub> hubContext,
        ILogger<SignalRBroadcastService> logger)
    {
        _banQueueService = banQueueService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[SignalRBridge] Starting SignalR broadcast bridge...");

        // 订阅 BanQueueService 事件
        _banQueueService.OnStatsUpdated += HandleStatsUpdated;
        _banQueueService.OnBatchComplete += HandleBatchComplete;
        _banQueueService.OnPlayerStatusChanged += HandlePlayerStatusChanged;

        _logger.LogInformation("[SignalRBridge] Event subscriptions registered");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[SignalRBridge] Stopping SignalR broadcast bridge...");

        // 取消订阅
        _banQueueService.OnStatsUpdated -= HandleStatsUpdated;
        _banQueueService.OnBatchComplete -= HandleBatchComplete;
        _banQueueService.OnPlayerStatusChanged -= HandlePlayerStatusChanged;

        return Task.CompletedTask;
    }

    private async void HandleStatsUpdated()
    {
        try
        {
            _logger.LogInformation("[SignalR] Broadcast StatsUpdated");
            await _hubContext.Clients.All.SendAsync("StatsUpdated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SignalR] Failed to broadcast StatsUpdated");
        }
    }

    private async void HandlePlayerStatusChanged(PlayerStatusChangedEventArgs args)
    {
        try
        {
            _logger.LogInformation("[SignalR] Broadcast PlayerStatusChanged for {PlayerId}", args.PlayerId);
            await _hubContext.Clients.All.SendAsync("PlayerStatusChanged", new
            {
                PlayerId = args.PlayerId.ToString(),
                Status = args.Status,
                BatchId = args.BatchId
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SignalR] Failed to broadcast PlayerStatusChanged");
        }
    }

    private async void HandleBatchComplete(BatchCompleteEventArgs args)
    {
        try
        {
            _logger.LogInformation("[SignalR] Broadcast BatchJobFinished: BatchId={BatchId}", args.BatchId);
            await _hubContext.Clients.All.SendAsync("BatchJobFinished", new
            {
                BatchId = args.BatchId,
                TotalCount = args.TotalCount,
                SuccessCount = args.SuccessCount,
                FailedCount = args.FailedCount
            });

            // 同时发送 StatsUpdated
            await _hubContext.Clients.All.SendAsync("StatsUpdated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SignalR] Failed to broadcast BatchJobFinished");
        }
    }
}
