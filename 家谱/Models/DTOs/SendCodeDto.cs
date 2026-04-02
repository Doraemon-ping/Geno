using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    public class SendCodeDto
    {
        // 发送验证码的DTO，包含邮箱地址
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
