using System.Text.Json.Serialization;

namespace GameAdmin.Application.DTOs;

/// <summary>
/// 玩家数据传输对象
/// </summary>
public record PlayerDto(
    Guid Id,
    string Nickname,
    int Level,
    /// <summary>
    /// 金币数量（序列化为字符串以防止前端精度丢失）
    /// </summary>
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    long Gold,
    bool IsBanned,
    DateTime CreatedAt
);
