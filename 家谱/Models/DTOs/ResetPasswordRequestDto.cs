namespace 家谱.Models.DTOs
{
    public class ResetPasswordRequestDto
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }
}
