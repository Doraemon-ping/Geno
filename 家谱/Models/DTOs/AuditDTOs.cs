namespace 家谱.Models.DTOs
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// 通用申请提交 DTO
    /// </summary>
    public class AuditApplyRequest
    {
        /// <summary>
        /// 审核类别：1-身份认领(LinkMember), 2-权限申请(RoleUpgrade), 
        /// </summary>
        [Required(ErrorMessage = "请选择申请类别")]
        [Range(1, 2)]
        public int Category { get; set; }

        /// <summary>
        /// 目标ID：认领成员时填 MemberID，权限申请可为空
        /// </summary>
        public Guid? TargetMemberID { get; set; }

        /// <summary>
        /// 申请理由
        /// </summary>
        [Required(ErrorMessage = "请填写申请理由")]
        [MaxLength(200)]
        public string? Reason { get; set; }

        /// <summary>
        /// 关键数据：例如权限申请 {"NewRole": "Admin"} 或 修改内容 {"NewName": "张三"}
        /// 前端将对象 JSON.stringify 后传给此字段
        /// </summary>
        public string PayloadJson { get; set; } = "{}";
    }

    /// <summary>
    /// 获取审核任务列表的响应 DTO
    /// 
    public class AuditTaskResponse
    {
        /// <summary>
        /// 业务流水号（由 Guid 转出的字符串）
        /// </summary>
        public string SerialNo { get; set; } = string.Empty;

        /// <summary>
        /// 申请人名称（需从 User 表关联）
        /// </summary>
        public string ApplicantName { get; set; } = string.Empty;

        /// <summary>
        /// 类别名称：身份认领 / 权限申请 / 
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// 目标对象描述：例如“认领：张三(第12代)”
        /// </summary>
        public string? TargetDescription { get; set; }

        public string? Reason { get; set; }

        /// <summary>
        /// 状态：0-待审, 1-通过, 2-拒绝
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 状态文本：待审核 / 已准许 / 已驳回
        /// </summary>
        public string StatusText { get; set; } = string.Empty;

        public string? AuditRemark { get; set; }

        public string CreatedAt { get; set; } = string.Empty;

        public string? ProcessedAt { get; set; }

        /// <summary>
        /// 原始 Payload 数据，供前端逻辑解析
        /// </summary>
        public string PayloadJson { get; set; } = string.Empty;
    }

    /// <summary>
    ///  处理审核请求 DTO
    /// </summary>
    public class HandleAuditRequest
    {
        /// <summary>
        /// 对应列表中的 SerialNo
        /// </summary>
        [Required]
        public string SerialNo { get; set; } = string.Empty;

        /// <summary>
        /// 操作结果：1-通过, 2-拒绝
        /// </summary>
        [Range(1, 2, ErrorMessage = "操作无效")]
        public int Action { get; set; }

        /// <summary>
        /// 审批备注
        /// </summary>
        [MaxLength(200)]
        public string? AuditRemark { get; set; }
    }


}
