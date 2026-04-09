using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 家谱.Common;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Enums;
using 家谱.Services;

namespace 家谱.Controllers
{
    /// <summary>
    /// 家谱成员控制器。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MemberController : ControllerBase
    {
        private readonly IGenoMemberService _memberService;
        private readonly IGenoTreeService _treeService;
        private readonly IReviewService _reviewService;
        private readonly ITreePermissionService _treePermissionService;

        public MemberController(
            IGenoMemberService memberService,
            IGenoTreeService treeService,
            IReviewService reviewService,
            ITreePermissionService treePermissionService)
        {
            _memberService = memberService;
            _treeService = treeService;
            _reviewService = reviewService;
            _treePermissionService = treePermissionService;
        }

        /// <summary>
        /// 获取树成员列表。
        /// </summary>
        [HttpGet("tree/{treeId}")]
        public async Task<IActionResult> GetList(Guid treeId)
        {
            var tree = await _treeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权限访问此资源");
            }

            var members = await _memberService.GetByTreeIdAsync(treeId);
            return Ok(ApiResponse.OK(members));
        }

        /// <summary>
        /// 新增树成员。
        /// </summary>
        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] GenoMemberDto dto)
        {
            var tree = await _treeService.GetByIdAsync(dto.TreeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权限访问此资源");
            }

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(tree.TreeID, currentUserId))
            {
                var member = await _memberService.CreateAsync(dto, currentUserId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "树成员添加成功",
                    Data = new { memberId = member.MemberID }
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                ActionCode = ReviewActions.MemberCreate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "普通用户提交新增树成员申请",
                ForceCreateTask = true
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "新增成员申请已提交，等待树拥有者或树管理员审核"
            }));
        }

        /// <summary>
        /// 修改树成员。
        /// </summary>
        [HttpPut("Update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] GenoMemberDto dto)
        {
            var member = await _memberService.GetByIdAsync(id) ?? throw new KeyNotFoundException("树成员不存在");
            if (member.TreeID != dto.TreeId)
            {
                throw new ArgumentException("不允许跨家谱树修改成员");
            }

            var tree = await _treeService.GetByIdAsync(dto.TreeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权限访问此资源");
            }

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(tree.TreeID, currentUserId))
            {
                var success = await _memberService.UpdateAsync(id, dto, currentUserId);
                if (!success)
                {
                    throw new InvalidOperationException("树成员更新失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "树成员已更新"
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                TargetId = id,
                ActionCode = ReviewActions.MemberUpdate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "普通用户提交树成员修改申请"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "成员修改申请已提交，等待树拥有者或树管理员审核"
            }));
        }

        /// <summary>
        /// 删除树成员。
        /// </summary>
        [HttpDelete("Del/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var member = await _memberService.GetByIdAsync(id) ?? throw new KeyNotFoundException("树成员不存在");
            var tree = await _treeService.GetByIdAsync(member.TreeID) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权限访问此资源");
            }

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(tree.TreeID, currentUserId))
            {
                var success = await _memberService.DeleteAsync(id, currentUserId);
                if (!success)
                {
                    throw new InvalidOperationException("树成员删除失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "树成员已删除"
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = member.TreeID,
                TargetId = id,
                ActionCode = ReviewActions.MemberDelete,
                ChangeData = JsonSerializer.Serialize(new
                {
                    member.MemberID,
                    member.TreeID,
                    member.LastName,
                    member.FirstName,
                    member.GenerationNum,
                    member.PoemID,
                    member.Gender,
                    member.BirthDateRaw,
                    member.Biography
                }, JsonDefaults.Options),
                Reason = "普通用户提交树成员删除申请"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "成员删除申请已提交，等待树拥有者或树管理员审核"
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

        private byte GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!byte.TryParse(roleClaim, out var role))
            {
                throw new UnauthorizedAccessException("无法解析当前用户角色");
            }

            return role;
        }
    }
}
