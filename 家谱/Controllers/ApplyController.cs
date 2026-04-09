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
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ApplyController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly GenealogyDbContext _db;

        public ApplyController(IReviewService reviewService, GenealogyDbContext db)
        {
            _reviewService = reviewService;
            _db = db;
        }

        [HttpPost("apply-admin")]
        public async Task<IActionResult> ApplyAdmin([FromBody] RoleApplyPayload dto)
        {
            var userId = GetCurrentUserId();
            if (dto.NewRole == 0)
                throw new ArgumentException("不允许申请!");
            if (dto.NewRole != 1 && dto.NewRole != 2)
                throw new Exception("只能申请有效权限");
            if (string.IsNullOrWhiteSpace(dto.TargetId))
                throw new Exception("目标修改用户不能为空");
            if (!Guid.TryParse(dto.TargetId, out var targetGuid))
                throw new ArgumentException("目标用户 ID 格式无效");

            var submitResult = new SubmitReviewRequest
            {
                TargetId = targetGuid,
                ActionCode = ReviewActions.ApplyAdmin,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = dto.Reason
            };

            await _reviewService.SubmitAsync(submitResult, userId);
            return Ok(ApiResponse.OK());
        }

        [HttpPost("apply-tree-role")]
        public async Task<IActionResult> ApplyTreeRole([FromBody] TreePermissionApplyDto dto)
        {
            var userId = GetCurrentUserId();
            if (dto.TreeId == Guid.Empty)
                throw new ArgumentException("目标家谱树不能为空");
            if (dto.NewRole != (byte)TreeRoleType.Admin && dto.NewRole != (byte)TreeRoleType.Editor)
                throw new ArgumentException("只能申请树管理员或修谱员权限");

            var targetUserId = dto.TargetUserId ?? userId;
            if (!await _db.GenoTrees.AnyAsync(t => t.TreeID == dto.TreeId && !t.IsDel))
                throw new Exception("家谱树不存在");
            if (!await _db.Users.AnyAsync(u => u.UserID == targetUserId && u.UserStatus == 1))
                throw new Exception("目标用户不存在");

            dto.TargetUserId = targetUserId;

            var submitResult = new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                TargetId = targetUserId,
                ActionCode = ReviewActions.TreeApplyRole,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = dto.Reason
            };

            var taskId = await _reviewService.SubmitAsync(submitResult, userId);
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
                throw new UnauthorizedAccessException("无法解析当前用户身份");

            return userId;
        }
    }
}
