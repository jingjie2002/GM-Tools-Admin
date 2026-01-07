using FluentValidation;
using GameAdmin.Application.DTOs;

namespace GameAdmin.Api.Validators;

/// <summary>
/// GiveItemRequest 验证器
/// </summary>
public class GiveItemRequestValidator : AbstractValidator<GiveItemRequest>
{
    public GiveItemRequestValidator()
    {
        RuleFor(x => x.PlayerId)
            .NotEmpty().WithMessage("玩家 ID 不能为空");

        RuleFor(x => x.ItemType)
            .NotEmpty().WithMessage("道具类型不能为空")
            .MaximumLength(50).WithMessage("道具类型长度不能超过 50 字符");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("金额必须大于 0")
            .LessThanOrEqualTo(1_000_000).WithMessage("单次发放不能超过 100 万");
    }
}

/// <summary>
/// ApproveItemRequest 验证器
/// </summary>
public class ApproveItemRequestValidator : AbstractValidator<ApproveItemRequest>
{
    public ApproveItemRequestValidator()
    {
        RuleFor(x => x.LogId)
            .NotEmpty().WithMessage("日志 ID 不能为空");
    }
}

/// <summary>
/// RejectItemRequest 验证器
/// </summary>
public class RejectItemRequestValidator : AbstractValidator<RejectItemRequest>
{
    public RejectItemRequestValidator()
    {
        RuleFor(x => x.LogId)
            .NotEmpty().WithMessage("日志 ID 不能为空");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("拒绝原因不能为空")
            .MinimumLength(5).WithMessage("拒绝原因至少 5 个字符")
            .MaximumLength(500).WithMessage("拒绝原因不能超过 500 字符");
    }
}
