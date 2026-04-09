namespace 家谱.Models.DTOs
{
    /// <summary>
    /// Defines the <see cref="TaskDtos" />
    /// Task接口相关的 DTOs 定义类，包含提交审核任务和获取审核任务列表等接口所需的 DTOs 定义
    /// </summary>
    public class TaskDtos
    {
        /// <summary>
        /// Gets or sets the TaskId 任务ID，唯一标识一个审核任务
        /// </summary>
        public Guid TaskId { get; set; }

        /// <summary>
        /// Gets or sets the SubmitterName 提交人名称，方便前端显示，实际存储中只保存 SubmitterID（用户ID），通过关联查询获取名称
        /// </summary>
        public string SubmitterName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ActionName 任务类型名称，如 "修改成员信息"、"申请管理员权限" 等，实际存储中只保存 ActionCode（如 "Member.Update"），通过映射关系获取名称
        /// </summary>
        public string ActionName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ChangeData 变更数据，存储为 JSON 字符串，前端可以根据 ActionCode 解析成具体的对象结构进行显示和处理
        /// </summary>
        public object ChangeData { get; set; } = null!;

        /// <summary>
        /// Gets or sets the Reason 申请理由，前端提交时由用户填写，存储在 ApplyReason 字段中，方便审核人查看审核动机和背景信息
        /// </summary>
        public string? Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Status 任务状态，0-待审核（Pending），1-审核通过（Approved），2-审核驳回（Rejected），前端可以根据这个状态显示不同的标签或颜色，实际存储中使用整数表示状态，前端通过映射关系获取名称和样式
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ReviewName 审核人名称，方便前端显示，实际存储中只保存 ReviewerID（用户ID），通过关联查询获取名称，如果任务未处理则该字段可以为空或显示为 "待分配" 等提示信息
        /// </summary>
        public string? ReviewName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ReviewNotes 审核备注，审核人处理任务时填写的备注信息，存储在 ReviewNotes 字段中，方便提交人和其他审核人查看审核过程中的沟通和说明，如果任务未处理则该字段可以为空或显示为 "无" 等提示信息
        /// </summary>
        public string ReviewNotes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CreateTime 任务创建时间，存储在 CreateTime 字段中，前端可以根据这个时间显示任务的提交时间，实际存储中使用 DateTime 类型，前端通过格式化显示为字符串
        /// </summary>
        public string CreateTime { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ProcessTime 任务处理时间，存储在 ProcessedAt 字段中，前端可以根据这个时间显示任务的审核完成时间，如果任务未处理则该字段可以为空或显示为 "待处理" 等提示信息，实际存储中使用 DateTime 类型，前端通过格式化显示为字符串
        /// </summary>
        public string ProcessTime { get; set; } = string.Empty;
    }
}
