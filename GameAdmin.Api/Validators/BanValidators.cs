using FluentValidation;
using GameAdmin.Application.DTOs;

namespace GameAdmin.Api.Validators;

/// <summary>
/// 封禁请求验证器
/// </summary>
public class BanPlayerRequestValidator : AbstractValidator<BanPlayerRequest>
{
    public BanPlayerRequestValidator()
    {
        RuleFor(x => x.PlayerId)
            .NotEmpty().WithMessage("玩家 ID 不能为空");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("封禁原因不能为空")
            .MinimumLength(2).WithMessage("原因至少 2 个字符")
            .MaximumLength(200).WithMessage("原因不能超过 200 字符");

        RuleFor(x => x.DurationHours)
            .GreaterThanOrEqualTo(0).WithMessage("封禁时长不能为负数")
            .LessThanOrEqualTo(87600).WithMessage("封禁时长最长为 10 年");
    }
}
