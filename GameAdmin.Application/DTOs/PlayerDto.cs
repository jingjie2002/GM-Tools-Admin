namespace GameAdmin.Application.DTOs;

/// <summary>
/// 玩家数据传输对象
/// </summary>
public record PlayerDto(
    Guid Id,
    string Nickname,
    int Level,
    long Gold,
    bool IsBanned,
    DateTime CreatedAt
);
