

namespace 家谱.Models.DTOs.Common
{
    // 1. 定义一个非泛型的基类
    public class ApiResponse
    {
        public string? Message { get; set; }
        public int Code { get; set; }
        public object Data { get; set; } = null!; // 占位字段，供非泛型版本使用

        public static ApiResponse OK()
            => new ApiResponse { Message = "操作成功！", Code = 200 };

        public static ApiResponse OK(object data)
            => new ApiResponse { Message = "操作成功！", Code = 200, Data = data };


    }
}