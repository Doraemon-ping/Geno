namespace 家谱.Models.DTOs
{
    public class TreePermissionApplyDto
    {
        public Guid TreeId { get; set; }

        public byte NewRole { get; set; }

        public string Reason { get; set; } = string.Empty;

        public Guid? TargetUserId { get; set; }
    }

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

    public class TreeAccessDto
    {
        public bool IsSuperAdmin { get; set; }

        public bool IsOwner { get; set; }

        public byte? RoleType { get; set; }

        public string RoleName { get; set; } = "访客";

        public bool CanEdit { get; set; }

        public bool CanReview { get; set; }

        public bool CanManagePermissions { get; set; }
    }

    public class WorkflowResultDto
    {
        public bool AppliedDirectly { get; set; }

        public bool SubmittedForReview { get; set; }

        public Guid? TaskId { get; set; }

        public string Message { get; set; } = string.Empty;

        public object? Data { get; set; }
    }
}
