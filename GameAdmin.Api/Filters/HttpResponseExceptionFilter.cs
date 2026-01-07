using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GameAdmin.Api.Filters;

/// <summary>
/// 全局异常过滤器
/// 业务异常（校验失败、参数错误）：返回具体消息，状态码 400
/// 系统异常（代码崩溃、数据库异常）：返回通用消息，状态码 500
/// 所有异常都会打印完整堆栈到控制台
/// </summary>
public class HttpResponseExceptionFilter : IExceptionFilter
{
    private readonly ILogger<HttpResponseExceptionFilter> _logger;

    public HttpResponseExceptionFilter(ILogger<HttpResponseExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;

        // === 强化日志：所有异常打印完整堆栈 ===
        Console.WriteLine($"[EXCEPTION] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"[EXCEPTION] Type: {exception.GetType().FullName}");
        Console.WriteLine($"[EXCEPTION] Message: {exception.Message}");
        Console.WriteLine($"[EXCEPTION] StackTrace:\n{exception.StackTrace}");

        // 记录到日志系统（包含完整异常对象）
        _logger.LogError(exception, "Unhandled exception occurred: {ExceptionType} - {Message}",
            exception.GetType().Name, exception.Message);

        // === 智能判断异常类型 ===
        var (statusCode, message) = ClassifyException(exception);

        // === 统一返回格式 ===
        var result = new ObjectResult(new
        {
            success = false,
            message = message
        })
        {
            StatusCode = statusCode
        };

        context.Result = result;
        context.ExceptionHandled = true;
    }

    /// <summary>
    /// 分类异常：区分业务校验错误与系统异常
    /// </summary>
    private static (int StatusCode, string Message) ClassifyException(Exception exception)
    {
        var exceptionTypeName = exception.GetType().FullName ?? exception.GetType().Name;

        // FluentValidation 校验失败
        if (exceptionTypeName.Contains("FluentValidation.ValidationException"))
        {
            return (StatusCodes.Status400BadRequest, FormatValidationErrors(exception));
        }

        // DataAnnotations 校验失败
        if (exceptionTypeName.Contains("System.ComponentModel.DataAnnotations.ValidationException"))
        {
            return (StatusCodes.Status400BadRequest, exception.Message);
        }

        // 其他业务异常
        return exception switch
        {
            ArgumentNullException nullEx =>
                (StatusCodes.Status400BadRequest, nullEx.Message),

            ArgumentException argEx =>
                (StatusCodes.Status400BadRequest, argEx.Message),

            // 业务逻辑错误（如"该审批正在处理中"）
            InvalidOperationException invalidEx =>
                (StatusCodes.Status400BadRequest, invalidEx.Message),

            // 资源未找到
            KeyNotFoundException notFoundEx =>
                (StatusCodes.Status404NotFound, notFoundEx.Message),

            // 权限不足
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "未授权访问"),

            // 系统异常：不暴露内部细节
            _ => (StatusCodes.Status500InternalServerError, "服务器忙，请稍后再试")
        };
    }

    /// <summary>
    /// 格式化校验错误消息
    /// </summary>
    private static string FormatValidationErrors(Exception exception)
    {
        // 使用反射获取 Errors 属性（避免直接引用 FluentValidation 类型）
        var errorsProperty = exception.GetType().GetProperty("Errors");
        if (errorsProperty?.GetValue(exception) is System.Collections.IEnumerable errors)
        {
            var messages = new List<string>();
            foreach (var error in errors)
            {
                var errorMessageProperty = error.GetType().GetProperty("ErrorMessage");
                if (errorMessageProperty?.GetValue(error) is string errorMessage)
                {
                    messages.Add(errorMessage);
                }
            }
            if (messages.Count > 0)
            {
                return string.Join("; ", messages);
            }
        }

        return exception.Message;
    }
}
