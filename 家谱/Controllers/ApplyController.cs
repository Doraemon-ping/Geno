using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 家谱.Common;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Enums;
using 家谱.Services;

namespace 家谱.Controllers
{
    /// <summary>
    /// 申请与授权控制器。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ApplyController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly ITreePermissionService _treePermissionService;
        private readonly IAuditLogService _auditLogService;
        private readonly GenealogyDbContext _db;

        public ApplyController(
            IReviewService reviewService,
            ITreePermissionService treePermissionService,
            IAuditLogService auditLogService,
            GenealogyDbContext db)
        {
            _reviewService = reviewService;
            _treePermissionService = treePermissionService;
            _auditLogService = auditLogService;
            _db = db;
        }

        /// <summary>
        /// 提交系统角色申请。
        /// </summary>
        [HttpPost("apply-admin")]
        public async Task<IActionResult> ApplyAdmin([FromBody] RoleApplyPayload dto)
        {
            var userId = GetCurrentUserId();
            var currentUser = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.UserID == userId && user.UserStatus == 1)
                ?? throw new KeyNotFoundException("当前用户不存在");

            if (dto.NewRole == (byte)RoleType.SuperAdmin)
            {
                throw new ArgumentException("不允许直接申请超级管理员");
            }

            if (dto.NewRole is not ((byte)RoleType.Admin) and not ((byte)RoleType.Editor))
            {
                throw new ArgumentException("只能申请有效的系统权限");
            }

            if (currentUser.RoleType == dto.NewRole)
            {
                throw new ArgumentException("当前账号已经拥有该系统角色，无需重复申请");
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TargetId = userId,
                ActionCode = ReviewActions.ApplyAdmin,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = dto.Reason
            }, userId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "系统权限申请已提交，等待审核"
            }));
        }

        /// <summary>
        /// 申请或分配树内权限。
        /// </summary>
        [HttpPost("apply-tree-role")]
        public async Task<IActionResult> ApplyTreeRole([FromBody] TreePermissionApplyDto dto)
        {
            var currentUserId = GetCurrentUserId();
            if (dto.TreeId == Guid.Empty)
            {
                throw new ArgumentException("目标家谱树不能为空");
            }

            if (dto.NewRole is not ((byte)TreeRoleType.Admin) and not ((byte)TreeRoleType.Editor))
            {
                throw new ArgumentException("只能申请树管理员或修谱员权限");
            }

            var tree = await _db.GenoTrees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TreeID == dto.TreeId && !item.IsDel)
                ?? throw new KeyNotFoundException("家谱树不存在");

            var targetUserId = dto.TargetUserId ?? currentUserId;
            var targetUser = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.UserID == targetUserId && user.UserStatus == 1)
                ?? throw new KeyNotFoundException("目标用户不存在");

            var access = await _treePermissionService.GetTreeAccessAsync(tree.TreeID, currentUserId);
            if (access.CanManagePermissions)
            {
                var existing = await _db.TreePermissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(permission => permission.TreeID == dto.TreeId && permission.UserID == targetUserId);

                var permission = await _treePermissionService.UpsertPermissionAsync(dto.TreeId, targetUserId, dto.NewRole, currentUserId);
                await _auditLogService.WriteAsync(
                    "Geno_Tree_Permissions",
                    permission.PermissionID,
                    "UPDATE",
                    currentUserId,
                    new
                    {
                        TreeID = dto.TreeId,
                        UserID = targetUserId,
                        OldRole = existing?.RoleType
                    },
                    new
                    {
                        permission.PermissionID,
                        permission.TreeID,
                        permission.UserID,
                        permission.RoleType,
                        permission.IsActive
                    });

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = $"已为 {targetUser.Username} 分配 {ReviewActions.GetTreeRoleDisplayName(dto.NewRole)} 权限",
                    Data = new { permissionId = permission.PermissionID }
                }));
            }

            if (!tree.IsPublic)
            {
                throw new UnauthorizedAccessException("私有树仅允许拥有者或树管理员分配权限");
            }

            dto.TargetUserId = targetUserId;
            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                TargetId = targetUserId,
                ActionCode = ReviewActions.TreeApplyRole,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = dto.Reason
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "树权限申请已提交，等待审核"
            }));
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("无法解析当前用户身份");
            }

            return userId;
        }
    }
}
