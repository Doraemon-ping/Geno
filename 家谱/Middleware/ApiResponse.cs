namespace 家谱.Middleware
{
    public class ApiResponse
    {
        // 这是一个统一的 API 响应格式类，所有接口返回的数据都应该包装成这个格式
        //api异常处理类，包含状态码、消息和数据

        public int Code { get; set; }

        public string Message { get; set; }

        public object? Data { get; set; }
    }
}
