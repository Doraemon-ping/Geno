using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 提交审核任务的请求体。
    /// </summary>
    public class SubmitReviewRequest
    {
        /// <summary>
        /// 关联的家谱树标识。
        /// </summary>
        public Guid? TreeId { get; set; }

        /// <summary>
        /// 审核动作代码。
        /// </summary>
        [Required(ErrorMessage = "业务指令不能为空")]
        public string ActionCode { get; set; } = string.Empty;

        /// <summary>
        /// 目标实体标识。
        /// </summary>
        public Guid? TargetId { get; set; }

        /// <summary>
        /// 申请理由。
        /// </summary>
        [MaxLength(200)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 变更内容 JSON。
        /// </summary>
        [Required(ErrorMessage = "变更数据不能为空")]
        public string ChangeData { get; set; } = string.Empty;

        /// <summary>
        /// 是否强制创建任务而忽略重复校验。
        /// </summary>
        public bool ForceCreateTask { get; set; }
    }
}
