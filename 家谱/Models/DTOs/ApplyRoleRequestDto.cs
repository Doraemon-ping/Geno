namespace 家谱.Models.DTOs
{
    // 用户提交申请时使用

    public class ApplyRoleRequestDto
    {
        public int TargetRole { get; set; }

        public string Reason { get; set; } = string.Empty;
    }

    // 管理员审核处理时使用

    public class HandleAuditDto
    {
        public Guid RequestId { get; set; }

        public int Action { get; set; }

        public string? AuditRemark { get; set; }
    }
}
