using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameAdmin.Infrastructure.Services;

/// <summary>
/// Redis 服务接口
/// </summary>
public interface IRedisService
{
    /// <summary>
    /// 添加 Token 到黑名单
    /// </summary>
    Task AddToBlacklistAsync(string token, TimeSpan expiry);

    /// <summary>
    /// 检查 Token 是否在黑名单中
    /// </summary>
    Task<bool> IsBlacklistedAsync(string token);

    /// <summary>
    /// 获取分布式锁
    /// </summary>
    Task<bool> AcquireLockAsync(string lockKey, string lockValue, TimeSpan expiry);

    /// <summary>
    /// 释放分布式锁
    /// </summary>
    Task<bool> ReleaseLockAsync(string lockKey, string lockValue);

    /// <summary>
    /// 获取缓存值
    /// </summary>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// 设置缓存值
    /// </summary>
    Task SetAsync(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// 将用户加入黑名单
    /// </summary>
    Task BlacklistUserAsync(Guid userId, TimeSpan expiry);

    /// <summary>
    /// 检查用户是否在黑名单中
    /// </summary>
    Task<bool> IsUserBlacklistedAsync(Guid userId);
}

/// <summary>
/// Redis 服务实现
/// </summary>
public class RedisService : IRedisService
{
    private readonly IDatabase _db;
    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<RedisService> _logger;
    private const string BlacklistPrefix = "token:blacklist:";
    private const string LockPrefix = "lock:";

    public RedisService(IConnectionMultiplexer connection, ILogger<RedisService> logger)
    {
        _connection = connection;
        _logger = logger;

        // 强制连接检查（仅日志，实际 Fail-Fast 在 Program.cs 处理）
        if (!_connection.IsConnected)
        {
            Console.WriteLine("[WARN] >>> Redis 初始连接未成功，将在首次操作时重试");
            _logger.LogWarning("Redis initial connection not established. IsConnected: {IsConnected}", _connection.IsConnected);
        }
        else
        {
            Console.WriteLine("[INFO] >>> Redis 连接成功！");
            _logger.LogInformation("Redis connected successfully. Endpoints: {Endpoints}",
                string.Join(",", _connection.GetEndPoints().Select(e => e.ToString())));
        }

        _db = connection.GetDatabase();
        _logger.LogInformation("Redis database obtained. Database: {Database}", _db.Database);
    }

    /// <summary>
    /// 检查 Redis 连接状态，未连接时抛出异常
    /// </summary>
    private void EnsureConnected()
    {
        if (!_connection.IsConnected)
        {
            _logger.LogError("Redis connection is not available!");
            throw new InvalidOperationException("Redis 连接不可用，请检查 Redis 服务状态");
        }
    }

    /// <inheritdoc />
    public async Task AddToBlacklistAsync(string token, TimeSpan expiry)
    {
        var key = $"{BlacklistPrefix}{token}";
        await _db.StringSetAsync(key, "1", expiry);
    }

    /// <inheritdoc />
    public async Task<bool> IsBlacklistedAsync(string token)
    {
        var key = $"{BlacklistPrefix}{token}";
        return await _db.KeyExistsAsync(key);
    }

    /// <inheritdoc />
    public async Task<bool> AcquireLockAsync(string lockKey, string lockValue, TimeSpan expiry)
    {
        // 快速短路：未连接时立即失败
        EnsureConnected();

        var key = $"{LockPrefix}{lockKey}";

        _logger.LogInformation("[探针] Redis AcquireLock - Key: {Key}, Value: {Value}, Expiry: {Expiry}s",
            key, lockValue, expiry.TotalSeconds);

        // 使用 SET NX (SetIfNotExists) 实现分布式锁
        // 如果 Redis 报错，让它直接抛出，不静默捕获
        var result = await _db.StringSetAsync(key, lockValue, expiry, When.NotExists);

        _logger.LogInformation("[探针] Redis AcquireLock 结果: {Result}", result);

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseLockAsync(string lockKey, string lockValue)
    {
        var key = $"{LockPrefix}{lockKey}";

        _logger.LogInformation("[探针] Redis ReleaseLock - Key: {Key}", key);

        // Lua 脚本保证原子性：只有持锁者才能释放锁
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";
        var result = (int)await _db.ScriptEvaluateAsync(script, [key], [lockValue]);

        _logger.LogInformation("[探针] Redis ReleaseLock 结果: {Result}", result);

        return result == 1;
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key)
    {
        return await _db.StringGetAsync(key);
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        if (expiry.HasValue)
        {
            await _db.StringSetAsync(key, value, expiry.Value);
        }
        else
        {
            await _db.StringSetAsync(key, value);
        }
    }

    /// <summary>
    /// 将用户加入黑名单 (踢下线)
    /// </summary>
    public async Task BlacklistUserAsync(Guid userId, TimeSpan expiry)
    {
        var key = $"{BlacklistPrefix}user:{userId}";
        await _db.StringSetAsync(key, "1", expiry);
    }

    /// <summary>
    /// 检查用户是否在黑名单中
    /// </summary>
    public async Task<bool> IsUserBlacklistedAsync(Guid userId)
    {
        var key = $"{BlacklistPrefix}user:{userId}";
        return await _db.KeyExistsAsync(key);
    }
}
