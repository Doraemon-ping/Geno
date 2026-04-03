namespace 家谱.Models.DTOs.Common
{
    // 1. 定义一个非泛型的基类
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int Code { get; set; }

        public static ApiResponse Fail(string msg)
            => new ApiResponse { Success = false, Message = msg, Code = 500 };

        public static ApiResponse Ok(string msg = "操作成功")
            => new ApiResponse { Success = true, Message = msg, Code = 200 };
    }

    // 2. 让你的泛型类继承它
    public class ApiResponse<T> : ApiResponse
    {
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T? data, string msg = "操作成功")
            => new ApiResponse<T> { Success = true, Data = data, Message = msg, Code = 200 };

        // 这里的 Fail 会覆盖基类的 Fail，并支持泛型上下文
        public new static ApiResponse<T> Fail(string msg)
            => new ApiResponse<T> { Success = false, Message = msg, Code = 500, Data = default };
    }
}