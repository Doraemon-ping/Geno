namespace 家谱.Models.Entities
{
    public class AuditRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // 申请人

        public Guid ApplicantId { get; set; }

        // 审核类别：1-身份认领(LinkMember), 2-权限申请(RoleUpgrade), 3-信息修改

        public int Category { get; set; }

        // 目标ID：比如认领家谱中的哪个人，就填那个MemberID

        public Guid? TargetMemberID { get; set; }

        // 存储变化的JSON数据（如新权限等级、修改后的姓名等）

        public string PayloadJson { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public int Status { get; set; } = 0;// 0-待审, 1-通过, 2-拒绝

        public string? AuditRemark { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }
    }
}
