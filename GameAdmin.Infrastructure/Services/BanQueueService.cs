using System.Threading.Channels;
using System.Collections.Concurrent;
using GameAdmin.Application.DTOs;
using GameAdmin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameAdmin.Infrastructure.Services;

/// <summary>
/// 系统繁忙异常 - 当队列满时抛出
/// </summary>
public class SystemBusyException : Exception
{
    public SystemBusyException() : base("系统繁忙，封禁队列已满，请稍后重试") { }
    public SystemBusyException(string message) : base(message) { }
}

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
/// 批量封禁请求进入队列后，后台多消费者并行处理，防止数据库瞬时锁死
/// </summary>
public class BanQueueService : BackgroundService
{
    private readonly Channel<BanQueueItem> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BanQueueService> _logger;

    // 并发消费者数量
    private const int ConsumerCount = 5;
    
    // 限流：每个消费者每秒处理 10 个（总计 50/s）
    private const int ProcessRatePerConsumer = 10;
    private readonly TimeSpan _processDelay = TimeSpan.FromMilliseconds(1000 / ProcessRatePerConsumer);

    // 批次进度追踪（线程安全）
    private readonly ConcurrentDictionary<string, BatchProgress> _batchProgress = new();

    // 事件通知（由 API 层订阅以触发 SignalR）
    public event Action? OnStatsUpdated;
    public event Action<BatchCompleteEventArgs>? OnBatchComplete;
    public event Action<PlayerStatusChangedEventArgs>? OnPlayerStatusChanged;

    public BanQueueService(IServiceProvider serviceProvider, ILogger<BanQueueService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // 使用 DropWrite 模式：队列满时立即失败，不阻塞 API 线程
        _channel = Channel.CreateBounded<BanQueueItem>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        });
    }

    /// <summary>
    /// 将封禁请求加入队列（非阻塞）
    /// </summary>
    /// <exception cref="SystemBusyException">当队列已满时抛出</exception>
    public Task<string> EnqueueBanAsync(BatchBanRequest request, Guid operatorId)
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

        var enqueuedCount = 0;
        foreach (var playerId in request.PlayerIds)
        {
            var item = new BanQueueItem
            {
                BatchId = batchId,
                PlayerId = playerId,
                Reason = request.Reason,
                DurationHours = request.DurationHours,
                OperatorId = operatorId
            };

            // TryWrite 立即返回，不阻塞
            if (!_channel.Writer.TryWrite(item))
            {
                // 队列已满，清理已创建的批次并抛出异常
                _batchProgress.TryRemove(batchId, out _);
                _logger.LogWarning("Ban queue is full! Rejected batch {BatchId} after {EnqueuedCount} items", batchId, enqueuedCount);
                throw new SystemBusyException($"系统繁忙，封禁队列已满（已入队 {enqueuedCount}/{request.PlayerIds.Count}），请稍后重试");
            }
            enqueuedCount++;
        }

        return Task.FromResult(batchId);
    }

    /// <summary>
    /// 后台消费者：启动多个并行消费者处理队列
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BanQueueService started with {ConsumerCount} parallel consumers, ~{Rate}/s total throughput", 
            ConsumerCount, ConsumerCount * ProcessRatePerConsumer);

        // 启动多个并行消费者
        var consumers = Enumerable.Range(0, ConsumerCount)
            .Select(i => ConsumeAsync(i, stoppingToken))
            .ToArray();

        await Task.WhenAll(consumers);
    }

    /// <summary>
    /// 单个消费者协程
    /// </summary>
    private async Task ConsumeAsync(int consumerId, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Consumer {ConsumerId} started", consumerId);

        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            bool success = false;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();

                var banRequest = new BanPlayerRequest(item.PlayerId, item.Reason, item.DurationHours);
                await playerService.BanPlayerAsync(banRequest, item.OperatorId, stoppingToken);

                _logger.LogDebug("Consumer {ConsumerId} - Batch {BatchId}: Banned player {PlayerId}", 
                    consumerId, item.BatchId, item.PlayerId);
                success = true;

                // 触发玩家状态变更事件（实时推送）
                OnPlayerStatusChanged?.Invoke(new PlayerStatusChangedEventArgs(item.PlayerId, "Banned", item.BatchId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumer {ConsumerId} - Batch {BatchId}: Failed to ban player {PlayerId}", 
                    consumerId, item.BatchId, item.PlayerId);
            }

            // 更新批次进度（线程安全）
            if (_batchProgress.TryGetValue(item.BatchId, out var progress))
            {
                Interlocked.Increment(ref progress.ProcessedCount);
                if (success) Interlocked.Increment(ref progress.SuccessCount);
                else Interlocked.Increment(ref progress.FailedCount);

                // 批次完成时触发事件
                if (progress.ProcessedCount >= progress.TotalCount)
                {
                    var args = new BatchCompleteEventArgs(item.BatchId, progress.TotalCount, progress.SuccessCount, progress.FailedCount);
                    OnBatchComplete?.Invoke(args);
                    _logger.LogInformation("Batch {BatchId} completed: Total={Total}, Success={Success}, Failed={Failed}",
                        item.BatchId, progress.TotalCount, progress.SuccessCount, progress.FailedCount);
                    _batchProgress.TryRemove(item.BatchId, out _);
                }
            }

            // 每个操作后触发更新事件
            OnStatsUpdated?.Invoke();

            // 限流延迟
            await Task.Delay(_processDelay, stoppingToken);
        }
        
        _logger.LogDebug("Consumer {ConsumerId} stopped", consumerId);
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
        public int TotalCount;
        public int ProcessedCount;
        public int SuccessCount;
        public int FailedCount;
    }
}


