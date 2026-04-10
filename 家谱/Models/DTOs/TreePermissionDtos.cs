namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 申请或分配树内权限的请求体。
    /// </summary>
    public class TreePermissionApplyDto
    {
        public Guid TreeId { get; set; }

        public byte NewRole { get; set; }

        public string Reason { get; set; } = string.Empty;

        public Guid? TargetUserId { get; set; }
    }

    /// <summary>
    /// 树权限展示对象。
    /// </summary>
    public class TreePermissionDto
    {
        public Guid PermissionId { get; set; }

        public Guid UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public byte RoleType { get; set; }

        public string RoleName { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public DateTime GrantedAt { get; set; }

        public bool IsOwner { get; set; }
    }

    /// <summary>
    /// 当前用户在树上的访问能力。
    /// </summary>
    public class TreeAccessDto
    {
        public bool IsSuperAdmin { get; set; }

        public bool IsSystemAdmin { get; set; }

        public bool IsOwner { get; set; }

        public byte? RoleType { get; set; }

        public string RoleName { get; set; } = "访客";

        public bool CanView { get; set; }

        public bool CanSubmitChange { get; set; }

        public bool CanDirectEdit { get; set; }

        // 兼容旧前端字段，语义等同于“可直接修改”。
        public bool CanEdit { get; set; }

        public bool CanReview { get; set; }

        public bool CanManagePermissions { get; set; }
    }

    /// <summary>
    /// 工作流处理结果。
    /// </summary>
    public class WorkflowResultDto
    {
        public bool AppliedDirectly { get; set; }

        public bool SubmittedForReview { get; set; }

        public Guid? TaskId { get; set; }

        public string Message { get; set; } = string.Empty;

        public object? Data { get; set; }
    }
}
