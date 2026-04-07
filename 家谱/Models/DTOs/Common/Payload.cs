namespace 家谱.Models.DTOs.Common
{
    //权限申请的 Payload 结构
    public class RoleApplyPayload
    {
        /// <summary>
        /// 目标角色：1-Admin, 2-Editor, 3-Viewer
        /// </summary>
        public byte NewRole { get; set; }

        public string Reason { get; set; } = string.Empty;// 申请理由

        public Guid TargetId { get; set; } // 目标对象 ID（如用户 ID 或家谱树 ID）
    }

}
