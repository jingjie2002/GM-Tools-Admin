using Microsoft.AspNetCore.Mvc.Filters;
using FluentValidation;

namespace GameAdmin.Api.Filters;

/// <summary>
/// 模型状态校验过滤器
/// 配合 SuppressModelStateInvalidFilter = true 使用
/// 当 ModelState 无效时，抛出 ValidationException，由全局异常过滤器捕获处理
/// </summary>
public class ValidateModelStateFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => new FluentValidation.Results.ValidationFailure(
                    propertyName: "",
                    errorMessage: e.ErrorMessage))
                .ToList();

            if (errors.Count != 0)
            {
                throw new ValidationException(errors);
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
