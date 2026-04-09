using System.ComponentModel.DataAnnotations;

public class SubmitReviewRequest
{
    public Guid? TreeId { get; set; }

    /// <summary>
    /// 对应 ReviewActions 中的常量，如 "Member.Update"
    /// </summary>
    [Required(ErrorMessage = "业务指令不能为空")]
    public string ActionCode { get; set; } = string.Empty;

    /// <summary>
    /// 操作目标（如 MemberID），新增操作可为 null
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// 申请理由
    /// </summary>
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 核心负载数据：可以是任何 JSON 结构
    /// 在 Controller 中使用 object 接收，由 Service 序列化存入 ChangeData
    /// </summary>
    [Required(ErrorMessage = "变更数据不能为空")]
    public string ChangeData { get; set; } = null!;

    public bool ForceCreateTask { get; set; }
}
