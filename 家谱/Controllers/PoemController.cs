using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 家谱.Common;
using 家谱.Middleware;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Enums;
using 家谱.Services;

namespace 家谱.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PoemController : ControllerBase
    {
        private readonly IGenoPoemService _poemService;
        private readonly IGenoTreeService _genoTreeService;
        private readonly IReviewService _reviewService;
        private readonly ITreePermissionService _treePermissionService;

        public PoemController(
            IGenoPoemService poemService,
            IGenoTreeService genoTreeService,
            IReviewService reviewService,
            ITreePermissionService treePermissionService)
        {
            _poemService = poemService;
            _genoTreeService = genoTreeService;
            _reviewService = reviewService;
            _treePermissionService = treePermissionService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("无法解析当前用户身份");

            return userId;
        }

        private byte GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!byte.TryParse(roleClaim, out var role))
                throw new UnauthorizedAccessException("无法解析当前用户角色");

            return role;
        }

        [HttpGet("tree/{treeId}")]
        public async Task<IActionResult> GetList(Guid treeId)
        {
            var tree = await _genoTreeService.GetByIdAsync(treeId);
            if (tree == null) return NotFound(new ErrorResponse { Code = 404, Message = "家谱树不存在" });

            var currentUserId = GetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
                throw new UnauthorizedAccessException("无权限访问此资源");

            var list = await _poemService.GetByTreeIdAsync(treeId);
            return Ok(new ApiResponse { Code = 200, Message = "获取成功", Data = list });
        }

        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] PoemDto dto)
        {
            var tree = await _genoTreeService.GetByIdAsync(dto.TreeId);
            if (tree == null) return NotFound(new ErrorResponse { Code = 404, Message = "家谱树不存在" });

            var currentUserId = GetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
                throw new UnauthorizedAccessException("无权限访问此资源");

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(tree.TreeID, currentUserId))
            {
                var poem = await _poemService.CreateAsync(dto, currentUserId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "字辈添加成功",
                    Data = new { poemId = poem.PoemID }
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                ActionCode = ReviewActions.PoemCreate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "普通用户提交新增字辈申请",
                ForceCreateTask = true
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "新增字辈申请已提交，等待审核"
            }));
        }

        [HttpPut("Update")]
        public async Task<IActionResult> Update([FromBody] PoemDto dto, Guid poemId)
        {
            var tree = await _genoTreeService.GetByIdAsync(dto.TreeId);
            if (tree == null) return NotFound(new ErrorResponse { Code = 404, Message = "家谱树不存在" });

            var currentUserId = GetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
                throw new UnauthorizedAccessException("无权限访问此资源");

            var poem = await _poemService.GetByIdAsync(poemId);
            if (poem == null) return NotFound(new ErrorResponse { Code = 404, Message = "字辈不存在" });
            if (poem.TreeID != dto.TreeId)
                return BadRequest(new ErrorResponse { Code = 400, Message = "不允许修改树ID，必须在同一棵树内修改" });

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(tree.TreeID, currentUserId))
            {
                await _poemService.UpdateAsync(dto, poemId, currentUserId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "字辈已更新"
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                TargetId = poemId,
                ActionCode = ReviewActions.PoemUpdate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "普通用户提交字辈修改申请"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "字辈修改申请已提交，等待审核"
            }));
        }

        [HttpDelete("Del/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var poem = await _poemService.GetByIdAsync(id);
            if (poem == null) return NotFound(new ErrorResponse { Code = 404, Message = "字辈不存在" });
            var tree = await _genoTreeService.GetByIdAsync(poem.TreeID);
            if (tree == null) return NotFound(new ErrorResponse { Code = 404, Message = "家谱树不存在" });

            var currentUserId = GetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
                throw new UnauthorizedAccessException("无权限访问此资源");

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(tree.TreeID, currentUserId))
            {
                await _poemService.DeleteAsync(id, currentUserId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "字辈删除成功"
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = tree.TreeID,
                TargetId = id,
                ActionCode = ReviewActions.PoemDelete,
                ChangeData = JsonSerializer.Serialize(new
                {
                    poem.PoemID,
                    poem.TreeID,
                    poem.GenerationNum,
                    poem.Word,
                    poem.Meaning
                }, JsonDefaults.Options),
                Reason = "普通用户提交字辈删除申请"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "字辈删除申请已提交，等待审核"
            }));
        }
    }
}
