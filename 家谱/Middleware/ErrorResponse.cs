namespace 家谱.Middleware
{
    public class ErrorResponse
    {
        // 这里的Code可以是HTTP状态码，也可以是你自定义的错误码
        // Message是错误信息，可以直接显示给用户或者用于前端的多语言处理
        //服端异常时返回的统一错误响应格式

        public int Code { get; set; }

        public string Message { get; set; }

    }
}
