namespace 家谱.Models.DTOs
{
    //= 用户数据传输对象 (DTO) ==
    public class UserDto
    {
        public Guid UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public byte RoleType { get; set; }
        public string RoleName => RoleType switch
        {
            1 => "管理员",
            2 => "修谱员",
            _ => "访客"
        };
        public DateTime CreatedAt { get; set; }
    }
}
