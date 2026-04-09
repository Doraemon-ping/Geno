namespace 家谱.Models.Enums
{
    public enum ReviewStatus : byte
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Canceled = 3,
    }

    public enum RoleType : byte
    {
        SuperAdmin = 0,
        Admin = 1,
        Editor = 2,
        Viewer = 3
    }

    public enum TreeRoleType : byte
    {
        Admin = 1,
        Editor = 2
    }

    public static class ReviewActions
    {
        public const string MemberUpdate = "Member.Update";
        public const string MemberIdentify = "Member.Identify";
        public const string TreeApplyRole = "Tree.ApplyRole";
        public const string TreeCreate = "Tree.Create";
        public const string TreeUpdate = "Tree.Update";
        public const string TreeDelete = "Tree.Delete";
        public const string PoemCreate = "Poem.Create";
        public const string PoemUpdate = "Poem.Update";
        public const string PoemDelete = "Poem.Delete";
        public const string MediaUpload = "Media.Upload";
        public const string ApplyAdmin = "ApplyAdmin";

        public static string GetDisplayName(string actionCode) => actionCode switch
        {
            ApplyAdmin => "系统权限申请",
            TreeApplyRole => "树权限申请",
            TreeCreate => "新建家谱树",
            TreeUpdate => "修改家谱树",
            TreeDelete => "删除家谱树",
            PoemCreate => "新增字辈",
            PoemUpdate => "修改字辈",
            PoemDelete => "删除字辈",
            MemberUpdate => "修改成员资料",
            MemberIdentify => "实名认领成员",
            MediaUpload => "上传影像资料",
            _ => actionCode
        };

        public static string GetRoleDisplayName(byte roleType) => roleType switch
        {
            (byte)RoleType.SuperAdmin => "超级管理员",
            (byte)RoleType.Admin => "管理员",
            (byte)RoleType.Editor => "修谱员",
            _ => "访客"
        };

        public static string GetTreeRoleDisplayName(byte roleType) => roleType switch
        {
            (byte)TreeRoleType.Admin => "树管理员",
            (byte)TreeRoleType.Editor => "修谱员",
            _ => "访客"
        };
    }
}
