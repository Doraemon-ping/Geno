namespace 家谱.Models.DTOs.Common
{
    //权限申请的 Payload 结构

    /// <summary>
    /// Defines the <see cref="RoleApplyPayload" />
    /// </summary>
    public class RoleApplyPayload
    {
        /// <summary>
        /// Gets or sets the NewRole
        /// 目标角色：1-Admin, 2-Editor, 3-Viewer
        /// </summary>
        public byte NewRole { get; set; }

        /// <summary>
        /// Gets or sets the Reason 申请理由，用户提交申请时填写的文本信息，存储在审核任务的 ApplyReason 字段中，方便审核人查看审核动机和背景信息，前端可以在申请界面提供一个文本输入框让用户填写申请理由，并在审核界面显示该理由供审核人参考
        /// </summary>
        public string Reason { get; set; } = string.Empty;// 申请理由

        /// <summary>
        /// Gets or sets the TargetId 目标提权对象 ID，指明申请权限的目标对象，如成员 ID 或家谱树 ID，具体含义根据 ActionCode 定义的业务场景而定，例如对于 "Tree.ApplyAdmin" 可能是家谱树 ID，对于 "Member.Update" 可能是成员 ID，前端在提交申请时需要根据业务场景传入正确的 TargetId，以便后端在处理审核任务时能够正确识别和修改对应的对象权限
        /// </summary>
        public string TargetId { get; set; } = string.Empty;// 目标对象 ID
    }

}
