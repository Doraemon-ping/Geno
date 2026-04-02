namespace 家谱.Models.DTOs
{
    public class UpdateProfileDto
    {
        public string? Username { get; set; } // 允许改昵称
        public string? Phone { get; set; }    // 修改联系方式
        public string? Email { get; set; }    // 修改邮箱
    }
}
