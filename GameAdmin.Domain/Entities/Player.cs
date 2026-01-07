namespace GameAdmin.Domain.Entities;

/// <summary>
/// 玩家实体 - 游戏管理系统核心领域对象
/// </summary>
public class Player
{
    /// <summary>
    /// 玩家唯一标识
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// 玩家昵称
    /// </summary>
    public required string Nickname { get; set; }

    /// <summary>
    /// 玩家等级
    /// </summary>
    public required int Level { get; set; }

    /// <summary>
    /// 玩家金币数量
    /// </summary>
    public required long Gold { get; set; }

    /// <summary>
    /// 是否被封禁
    /// </summary>
    public required bool IsBanned { get; set; }

    /// <summary>
    /// 封禁原因
    /// </summary>
    public string? BanReason { get; set; }

    /// <summary>
    /// 封禁截止时间 (null表示永久)
    /// </summary>
    public DateTime? BanExpiresAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// 创建新玩家的工厂方法
    /// </summary>
    public static Player Create(string nickname, int level = 1, long gold = 0)
    {
        return new Player
        {
            Id = Guid.NewGuid(),
            Nickname = nickname,
            Level = level,
            Gold = gold,
            IsBanned = false,
            CreatedAt = DateTime.UtcNow
        };
    }
}
