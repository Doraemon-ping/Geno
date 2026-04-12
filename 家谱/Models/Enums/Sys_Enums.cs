namespace 家谱.Models.Enums
{
    /// <summary>
    /// 审核任务状态。
    /// </summary>
    public enum ReviewStatus : byte
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Canceled = 3
    }

    /// <summary>
    /// 系统用户角色。
    /// </summary>
    public enum RoleType : byte
    {
        SuperAdmin = 0,
        Admin = 1,
        Editor = 2,
        Viewer = 3
    }

    /// <summary>
    /// 树内角色。
    /// </summary>
    public enum TreeRoleType : byte
    {
        Admin = 1,
        Editor = 2
    }

    /// <summary>
    /// 审核处理动作。
    /// </summary>
    public enum ReviewProcessAction : byte
    {
        Approve = 1,
        Reject = 2
    }

    /// <summary>
    /// 历史事件类型。
    /// </summary>
    public enum GenoEventType : byte
    {
        Birth = 1,
        Death = 2,
        Migration = 3,
        War = 4,
        Honor = 5,
        Education = 6
    }

    /// <summary>
    /// 媒体文件状态。
    /// </summary>
    public enum MediaFileStatus : byte
    {
        Temp = 0,
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Archived = 4
    }

    /// <summary>
    /// 审核动作代码与展示文案。
    /// </summary>
    public static class ReviewActions
    {
        public const string ApplyAdmin = "ApplyAdmin";
        public const string TreeApplyRole = "Tree.ApplyRole";
        public const string TreeCreate = "Tree.Create";
        public const string TreeUpdate = "Tree.Update";
        public const string TreeDelete = "Tree.Delete";
        public const string PoemCreate = "Poem.Create";
        public const string PoemUpdate = "Poem.Update";
        public const string PoemDelete = "Poem.Delete";
        public const string MemberCreate = "Member.Create";
        public const string MemberUpdate = "Member.Update";
        public const string MemberDelete = "Member.Delete";
        public const string MemberIdentify = "Member.Identify";
        public const string UnionCreate = "Union.Create";
        public const string UnionUpdate = "Union.Update";
        public const string UnionDelete = "Union.Delete";
        public const string UnionMemberAdd = "UnionMember.Add";
        public const string UnionMemberDelete = "UnionMember.Delete";
        public const string EventCreate = "Event.Create";
        public const string EventUpdate = "Event.Update";
        public const string EventDelete = "Event.Delete";
        public const string MediaUpload = "Media.Upload";

        /// <summary>
        /// 获取审核动作名称。
        /// </summary>
        public static string GetDisplayName(string actionCode) => actionCode switch
        {
            ApplyAdmin => "申请系统权限",
            TreeApplyRole => "申请树内权限",
            TreeCreate => "新建家谱树",
            TreeUpdate => "修改家谱树",
            TreeDelete => "删除家谱树",
            PoemCreate => "新增字辈",
            PoemUpdate => "修改字辈",
            PoemDelete => "删除字辈",
            MemberCreate => "新增树成员",
            MemberUpdate => "修改树成员",
            MemberDelete => "删除树成员",
            MemberIdentify => "认领树成员",
            UnionCreate => "新增婚姻单元",
            UnionUpdate => "修改婚姻单元",
            UnionDelete => "删除婚姻单元",
            UnionMemberAdd => "新增家庭子女关联",
            UnionMemberDelete => "删除家庭子女关联",
            EventCreate => "新增历史事件",
            EventUpdate => "修改历史事件",
            EventDelete => "删除历史事件",
            MediaUpload => "上传资料",
            _ => actionCode
        };

        /// <summary>
        /// 获取系统角色名称。
        /// </summary>
        public static string GetRoleDisplayName(byte roleType) => roleType switch
        {
            (byte)RoleType.SuperAdmin => "超级管理员",
            (byte)RoleType.Admin => "系统管理员",
            (byte)RoleType.Editor => "修谱员",
            _ => "访客"
        };

        /// <summary>
        /// 获取树内角色名称。
        /// </summary>
        public static string GetTreeRoleDisplayName(byte roleType) => roleType switch
        {
            (byte)TreeRoleType.Admin => "树管理员",
            (byte)TreeRoleType.Editor => "修谱员",
            _ => "普通成员"
        };

        /// <summary>
        /// 获取婚姻类型名称。
        /// </summary>
        public static string GetUnionTypeDisplayName(byte unionType) => unionType switch
        {
            1 => "正式结婚",
            2 => "事实婚姻",
            3 => "离异",
            _ => "未知类型"
        };

        /// <summary>
        /// 获取家庭子女关系名称。
        /// </summary>
        public static string GetUnionMemberRelationDisplayName(byte relType) => relType switch
        {
            1 => "亲生",
            2 => "收养",
            3 => "继子",
            4 => "过继",
            _ => "未知关系"
        };

        /// <summary>
        /// 获取事件类型名称。
        /// </summary>
        public static string GetEventTypeDisplayName(byte eventType) => eventType switch
        {
            (byte)GenoEventType.Birth => "出生",
            (byte)GenoEventType.Death => "逝世",
            (byte)GenoEventType.Migration => "迁徙",
            (byte)GenoEventType.War => "战争",
            (byte)GenoEventType.Honor => "荣誉",
            (byte)GenoEventType.Education => "教育",
            _ => "未知类型"
        };

        /// <summary>
        /// 获取媒体状态名称。
        /// </summary>
        public static string GetMediaStatusDisplayName(byte status) => status switch
        {
            (byte)MediaFileStatus.Temp => "临时上传",
            (byte)MediaFileStatus.Pending => "待审核",
            (byte)MediaFileStatus.Approved => "已通过",
            (byte)MediaFileStatus.Rejected => "已驳回",
            (byte)MediaFileStatus.Archived => "已归档",
            _ => "未知状态"
        };
    }
}
