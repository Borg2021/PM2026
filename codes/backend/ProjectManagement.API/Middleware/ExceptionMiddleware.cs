using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ProjectManagement.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未处理异常: {Message} | Path={Path} | Method={Method}",
                ex.Message, context.Request.Path, context.Request.Method);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.OK;

            var response = new
            {
                code = ex switch
                {
                    InvalidOperationException => 400,
                    UnauthorizedAccessException => 401,
                    _ => -1
                },
                message = ex switch
                {
                    UnauthorizedAccessException => "没有权限执行此操作",
                    var e when e.GetType().Name == "BadHttpRequestException" => $"上传文件大小超过系统限制（最大 {UploadConfig.CurrentLimit / 1024 / 1024} MB），请压缩后重试",
                    _ => ex.Message
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
