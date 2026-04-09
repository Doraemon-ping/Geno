namespace 家谱.Middleware
{
    using System.Net;
    using System.Text.Json;

    public class ExceptionMiddleware
    {
        // 这是一个全局异常处理中间件，用于捕获未处理的异常并返回统一的错误响应

        private readonly RequestDelegate _next;

        private readonly ILogger<ExceptionMiddleware> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context); // 继续执行后面的 Pipeline
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "捕获到未处理的异常"); // 记录日志
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = exception switch
            {
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                _ => (int)HttpStatusCode.InternalServerError
            };

            context.Response.ContentType = "application/json; charset=utf-8"; // 明确指定 UTF-8
            context.Response.StatusCode = statusCode;

            var response = new ErrorResponse
            {
                Code = statusCode,
                Message = exception.Message
            };

            // 配置：不进行 Unicode 编码，并使用小驼峰

            var json = JsonSerializer.Serialize(response, _jsonOptions);
            await context.Response.WriteAsync(json);
        }
    }
}
