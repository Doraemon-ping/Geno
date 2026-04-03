using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    //= 用户注册 DTO ==
    public class UserRegisterDto
    {
        [Required(ErrorMessage = "用户名是必填项")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "用户名长度需在1-50之间")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码是必填项")]
        [MinLength(1, ErrorMessage = "密码不能为空")]
        public string Password { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string? Email { get; set; }

        public string? Phone { get; set; }
    }
}
