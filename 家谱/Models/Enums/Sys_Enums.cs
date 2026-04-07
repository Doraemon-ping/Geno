namespace 家谱.Models.Enums
{
    public enum ReviewStatus : byte
    {
        Pending = 0,   // 待处理
        Approved = 1,  // 已通过
        Rejected = 2,  // 已拒绝
        Canceled = 3, // 已撤回
    }

    public enum RoleType : byte
    {
        Administratir = 0, // 超级管理员（系统级权限，无法删除或修改）
        Admin = 1,    // 管理员
        Editor = 2,   // 编辑者
        Viewer = 3    // 观察者
    }



    /// <summary>
    /// 预定义的 ActionCode 常量，防止硬编码字符串出错
    /// </summary>
    public static class ReviewActions
    {
        public const string MemberUpdate = "Member.Update";       // 修改成员资料
        public const string MemberIdentify = "Member.Identify";   // 实名认领成员
        public const string TreeApplyRole = "Tree.ApplyRole";     // 申请树权限
        public const string MediaUpload = "Media.Upload";         // 上传影像资料
        public const string PoemEdit = "Poem.Edit";             // 编辑家谱诗词
        public const string ApplyAdmin = "ApplyAdmin";                  // 申请管理员权限
    }






}