namespace GameAdmin.Application.DTOs;

/// <summary>
/// 分页结果
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>
/// 玩家查询参数
/// </summary>
public record PlayerQueryDto(
    int Page = 1,
    int PageSize = 10,
    string? Keyword = null
);
