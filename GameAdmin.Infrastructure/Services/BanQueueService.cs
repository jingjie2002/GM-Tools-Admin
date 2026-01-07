using System.Threading.Channels;
using GameAdmin.Application.DTOs;
using GameAdmin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameAdmin.Infrastructure.Services;

/// <summary>
/// 批次完成事件数据
/// </summary>
public record BatchCompleteEventArgs(string BatchId, int TotalCount, int SuccessCount, int FailedCount);

/// <summary>
/// 玩家状态变更事件数据
/// </summary>
public record PlayerStatusChangedEventArgs(Guid PlayerId, string Status, string BatchId);

/// <summary>
/// 封禁队列服务 - 使用 Channel 实现生产者-消费者模式
/// 批量封禁请求进入队列后，后台以 50/s 速度处理，防止数据库瞬时锁死
/// </summary>
public class BanQueueService : BackgroundService
{
    private readonly Channel<BanQueueItem> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BanQueueService> _logger;

    // 限流：每秒处理 50 个
    private const int ProcessRatePerSecond = 50;
    private readonly TimeSpan _processDelay = TimeSpan.FromMilliseconds(1000 / ProcessRatePerSecond);

    // 批次进度追踪
    private readonly Dictionary<string, BatchProgress> _batchProgress = new();

    // 事件通知（由 API 层订阅以触发 SignalR）
    public event Action? OnStatsUpdated;
    public event Action<BatchCompleteEventArgs>? OnBatchComplete;
    public event Action<PlayerStatusChangedEventArgs>? OnPlayerStatusChanged;

    public BanQueueService(IServiceProvider serviceProvider, ILogger<BanQueueService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _channel = Channel.CreateBounded<BanQueueItem>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// 将封禁请求加入队列
    /// </summary>
    public async Task<string> EnqueueBanAsync(BatchBanRequest request, Guid operatorId)
    {
        var batchId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("Batch ban enqueued: BatchId={BatchId}, Count={Count}", batchId, request.PlayerIds.Count);

        // 初始化批次进度
        _batchProgress[batchId] = new BatchProgress
        {
            TotalCount = request.PlayerIds.Count,
            ProcessedCount = 0,
            SuccessCount = 0,
            FailedCount = 0
        };

        foreach (var playerId in request.PlayerIds)
        {
            await _channel.Writer.WriteAsync(new BanQueueItem
            {
                BatchId = batchId,
                PlayerId = playerId,
                Reason = request.Reason,
                DurationHours = request.DurationHours,
                OperatorId = operatorId
            });
        }

        return batchId;
    }

    /// <summary>
    /// 后台消费者：按限流速度处理队列
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BanQueueService started, processing at {Rate}/s", ProcessRatePerSecond);

        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            bool success = false;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();

                var banRequest = new BanPlayerRequest(item.PlayerId, item.Reason, item.DurationHours);
                await playerService.BanPlayerAsync(banRequest, item.OperatorId, stoppingToken);

                _logger.LogDebug("Batch {BatchId}: Banned player {PlayerId}", item.BatchId, item.PlayerId);
                success = true;

                // 触发玩家状态变更事件（实时推送）
                OnPlayerStatusChanged?.Invoke(new PlayerStatusChangedEventArgs(item.PlayerId, "Banned", item.BatchId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch {BatchId}: Failed to ban player {PlayerId}", item.BatchId, item.PlayerId);
            }

            // 更新批次进度
            if (_batchProgress.TryGetValue(item.BatchId, out var progress))
            {
                progress.ProcessedCount++;
                if (success) progress.SuccessCount++;
                else progress.FailedCount++;

                // 批次完成时触发事件
                if (progress.ProcessedCount >= progress.TotalCount)
                {
                    var args = new BatchCompleteEventArgs(item.BatchId, progress.TotalCount, progress.SuccessCount, progress.FailedCount);
                    OnBatchComplete?.Invoke(args);
                    _logger.LogInformation("Batch {BatchId} completed: Total={Total}, Success={Success}, Failed={Failed}",
                        item.BatchId, progress.TotalCount, progress.SuccessCount, progress.FailedCount);
                    _batchProgress.Remove(item.BatchId);
                }
            }

            // 每个操作后触发更新事件
            OnStatsUpdated?.Invoke();

            // 限流延迟 + 强制日志刷新
            await Task.Yield();
            await Task.Delay(_processDelay, stoppingToken);
        }
    }

    /// <summary>
    /// 队列项
    /// </summary>
    private class BanQueueItem
    {
        public required string BatchId { get; init; }
        public required Guid PlayerId { get; init; }
        public required string Reason { get; init; }
        public required int DurationHours { get; init; }
        public required Guid OperatorId { get; init; }
    }

    private class BatchProgress
    {
        public int TotalCount { get; set; }
        public int ProcessedCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
    }
}


