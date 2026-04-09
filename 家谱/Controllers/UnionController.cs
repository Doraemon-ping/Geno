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
    /// 婚姻单元控制器。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UnionController : ControllerBase
    {
        private readonly IGenoUnionService _unionService;
        private readonly IUnionGraphService _unionGraphService;
        private readonly IGenoTreeService _treeService;
        private readonly IGenoMemberService _memberService;
        private readonly IReviewService _reviewService;
        private readonly ITreePermissionService _treePermissionService;

        public UnionController(
            IGenoUnionService unionService,
            IUnionGraphService unionGraphService,
            IGenoTreeService treeService,
            IGenoMemberService memberService,
            IReviewService reviewService,
            ITreePermissionService treePermissionService)
        {
            _unionService = unionService;
            _unionGraphService = unionGraphService;
            _treeService = treeService;
            _memberService = memberService;
            _reviewService = reviewService;
            _treePermissionService = treePermissionService;
        }

        /// <summary>
        /// 获取树下的婚姻单元列表。
        /// </summary>
        [HttpGet("tree/{treeId}")]
        public async Task<IActionResult> GetList(Guid treeId)
        {
            var tree = await _treeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问此资源");
            }

            var unions = await _unionService.GetByTreeIdAsync(treeId);
            return Ok(ApiResponse.OK(unions));
        }

        /// <summary>
        /// 获取婚姻单元树图数据。
        /// </summary>
        [AllowAnonymous]
        [HttpGet("graph/{treeId}")]
        public async Task<IActionResult> GetGraph(Guid treeId)
        {
            var tree = await _treeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = TryGetCurrentUserId();

            if (!tree.IsPublic)
            {
                if (!currentUserId.HasValue)
                {
                    throw new UnauthorizedAccessException("无权访问此资源");
                }

                if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId.Value))
                {
                    throw new UnauthorizedAccessException("无权访问此资源");
                }
            }

            var graph = await _unionGraphService.BuildTreeGraphAsync(treeId);
            return Ok(ApiResponse.OK(graph));
        }

        /// <summary>
        /// 新增婚姻单元。
        /// </summary>
        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] GenoUnionDto dto)
        {
            var tree = await _treeService.GetByIdAsync(dto.TreeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问此资源");
            }

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(dto.TreeId, currentUserId))
            {
                var union = await _unionService.CreateAsync(dto, currentUserId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "婚姻单元创建成功",
                    Data = new { unionId = union.UnionID }
                }));
            }

            var partner1 = await _memberService.GetByIdAsync(dto.Partner1Id) ?? throw new KeyNotFoundException("伴侣 1 不存在");
            var partner2 = await _memberService.GetByIdAsync(dto.Partner2Id) ?? throw new KeyNotFoundException("伴侣 2 不存在");

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                ActionCode = ReviewActions.UnionCreate,
                ChangeData = JsonSerializer.Serialize(new
                {
                    dto.TreeId,
                    dto.Partner1Id,
                    Partner1Name = $"{partner1.LastName}{partner1.FirstName}",
                    dto.Partner2Id,
                    Partner2Name = $"{partner2.LastName}{partner2.FirstName}",
                    dto.UnionType,
                    UnionTypeName = ReviewActions.GetUnionTypeDisplayName(dto.UnionType),
                    dto.SortOrder,
                    dto.MarriageDate
                }, JsonDefaults.Options),
                Reason = "普通用户提交新增婚姻单元申请",
                ForceCreateTask = true
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "新增婚姻单元申请已提交，等待审核"
            }));
        }

        /// <summary>
        /// 删除婚姻单元。
        /// </summary>
        [HttpDelete("Del/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var union = await _unionService.GetByIdAsync(id) ?? throw new KeyNotFoundException("婚姻单元不存在");
            var tree = await _treeService.GetByIdAsync(union.TreeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问此资源");
            }

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(union.TreeId, currentUserId))
            {
                var success = await _unionService.DeleteAsync(id, currentUserId);
                if (!success)
                {
                    throw new InvalidOperationException("婚姻单元删除失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "婚姻单元删除成功"
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = union.TreeId,
                TargetId = id,
                ActionCode = ReviewActions.UnionDelete,
                ChangeData = JsonSerializer.Serialize(union, JsonDefaults.Options),
                Reason = "普通用户提交删除婚姻单元申请"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "删除婚姻单元申请已提交，等待审核"
            }));
        }

        /// <summary>
        /// 添加家庭子女关联。
        /// </summary>
        [HttpPost("member/Add")]
        public async Task<IActionResult> AddMember([FromBody] GenoUnionMemberDto dto)
        {
            var tree = await _treeService.GetByIdAsync(dto.TreeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问此资源");
            }

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(dto.TreeId, currentUserId))
            {
                var relation = await _unionService.AddMemberAsync(dto, currentUserId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "家庭子女关联添加成功",
                    Data = new { relation.UnionID, relation.MemberID }
                }));
            }

            var child = await _memberService.GetByIdAsync(dto.MemberId) ?? throw new KeyNotFoundException("目标成员不存在");
            var union = await _unionService.GetByIdAsync(dto.UnionId) ?? throw new KeyNotFoundException("婚姻单元不存在");

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                TargetId = dto.MemberId,
                ActionCode = ReviewActions.UnionMemberAdd,
                ChangeData = JsonSerializer.Serialize(new
                {
                    dto.TreeId,
                    dto.UnionId,
                    union.Partner1,
                    union.Partner2,
                    dto.MemberId,
                    ChildName = $"{child.LastName}{child.FirstName}",
                    dto.RelType,
                    RelTypeName = ReviewActions.GetUnionMemberRelationDisplayName(dto.RelType),
                    dto.ChildOrder
                }, JsonDefaults.Options),
                Reason = "普通用户提交新增家庭子女关联申请",
                ForceCreateTask = true
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "新增家庭子女关联申请已提交，等待审核"
            }));
        }

        /// <summary>
        /// 删除家庭子女关联。
        /// </summary>
        [HttpDelete("member/Del")]
        public async Task<IActionResult> DeleteMember([FromQuery] Guid unionId, [FromQuery] Guid memberId)
        {
            var union = await _unionService.GetByIdAsync(unionId) ?? throw new KeyNotFoundException("婚姻单元不存在");
            var tree = await _treeService.GetByIdAsync(union.TreeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var child = await _memberService.GetByIdAsync(memberId) ?? throw new KeyNotFoundException("目标成员不存在");
            var currentUserId = GetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问此资源");
            }

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(union.TreeId, currentUserId))
            {
                var success = await _unionService.RemoveMemberAsync(unionId, memberId, currentUserId);
                if (!success)
                {
                    throw new InvalidOperationException("家庭子女关联删除失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "家庭子女关联删除成功"
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = union.TreeId,
                TargetId = memberId,
                ActionCode = ReviewActions.UnionMemberDelete,
                ChangeData = JsonSerializer.Serialize(new
                {
                    union.TreeId,
                    union.UnionId,
                    union.Partner1,
                    union.Partner2,
                    MemberId = memberId,
                    ChildName = $"{child.LastName}{child.FirstName}"
                }, JsonDefaults.Options),
                Reason = "普通用户提交删除家庭子女关联申请"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "删除家庭子女关联申请已提交，等待审核"
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

        private Guid? TryGetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
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
