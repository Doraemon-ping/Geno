using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    /// <summary>
    /// 系统审核任务表：支持功能指令化映射
    /// </summary>
    [Table("Sys_Review_Tasks")]
    public class ReviewTask
    {
        [Key]
        public Guid TaskID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 所属家谱树 ID
        /// </summary>
        public Guid? TreeID { get; set; }

        /// <summary>
        /// 申请人（提交者）ID
        /// </summary>
        [Required]
        public Guid SubmitterID { get; set; }

        /// <summary>
        /// 业务功能代码
        /// 示例：'Member.Update' (改资料), 'Tree.ApplyAdmin' (升限), 'Member.Identify' (实名认领)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ActionCode { get; set; } = string.Empty;

        /// <summary>
        /// 操作的目标对象 ID（如 MemberID 或 TreeID）
        /// 对于新增操作，此字段可为 Null
        /// </summary>
        public Guid? TargetID { get; set; }

        /// <summary>
        /// 变更数据的 JSON 字符串
        /// 存储如：{"RealName":"张三", "Avatar":"/url", "NewRole":1}
        /// </summary>
        [Required]
        public string ChangeData { get; set; } = string.Empty;

        /// <summary>
        /// 审核状态：0-待审, 1-通过, 2-拒绝, 3-用户撤回
        /// </summary>
        public byte Status { get; set; } = 0;

        /// <summary>
        /// 审核人 ID
        /// </summary>
        public Guid? ReviewerID { get; set; }

        /// <summary>
        /// 审核备注（拒绝理由或通过说明）
        /// </summary>
        [MaxLength(500)]
        public string? ReviewNotes { get; set; }

        /// <summary>
        /// 申请理由（由申请人填写）
        /// </summary>
        [MaxLength(200)]
        public string? ApplyReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ProcessedAt { get; set; }

        #region 导航属性

        [ForeignKey(nameof(SubmitterID))]
        public virtual SysUser Submitter { get; set; } = null!;

        [ForeignKey(nameof(ReviewerID))]
        public virtual SysUser? Reviewer { get; set; }

        [ForeignKey(nameof(TreeID))]
        public virtual GenoTree? Tree { get; set; }

        #endregion
    }
}
