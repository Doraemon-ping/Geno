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
    /// 字辈控制器。
    /// </summary>
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

        [AllowAnonymous]
        [HttpGet("tree/{treeId}")]
        public async Task<IActionResult> GetList(Guid treeId)
        {
            var tree = await _genoTreeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = TryGetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问该家谱树");
            }

            var list = await _poemService.GetByTreeIdAsync(treeId);
            return Ok(ApiResponse.OK(list));
        }

        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] PoemDto dto)
        {
            var tree = await _genoTreeService.GetByIdAsync(dto.TreeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();
            var access = await _treePermissionService.GetTreeAccessAsync(tree.TreeID, currentUserId);

            if (!access.CanView)
            {
                throw new UnauthorizedAccessException("无权访问该家谱树");
            }

            if (access.CanDirectEdit)
            {
                var poem = await _poemService.CreateAsync(dto, currentUserId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "字辈添加成功",
                    Data = new { poemId = poem.PoemID }
                }));
            }

            if (!access.CanSubmitChange)
            {
                throw new UnauthorizedAccessException("当前身份不允许提交字辈变更申请");
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                ActionCode = ReviewActions.PoemCreate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "修谱员提交新增字辈申请",
                ForceCreateTask = true
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "新增字辈申请已提交，等待树拥有者或树管理员审核"
            }));
        }

        [HttpPut("Update")]
        public async Task<IActionResult> Update([FromBody] PoemDto dto, Guid poemId)
        {
            var tree = await _genoTreeService.GetByIdAsync(dto.TreeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();
            var access = await _treePermissionService.GetTreeAccessAsync(tree.TreeID, currentUserId);

            if (!access.CanView)
            {
                throw new UnauthorizedAccessException("无权访问该家谱树");
            }

            var poem = await _poemService.GetByIdAsync(poemId) ?? throw new KeyNotFoundException("字辈不存在");
            if (poem.TreeID != dto.TreeId)
            {
                throw new ArgumentException("不允许修改树 ID，必须在同一棵树内修改");
            }

            if (access.CanDirectEdit)
            {
                var success = await _poemService.UpdateAsync(dto, poemId, currentUserId);
                if (!success)
                {
                    throw new Exception("字辈更新失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "字辈已更新"
                }));
            }

            if (!access.CanSubmitChange)
            {
                throw new UnauthorizedAccessException("当前身份不允许提交字辈修改申请");
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                TargetId = poemId,
                ActionCode = ReviewActions.PoemUpdate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "修谱员提交字辈修改申请"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "字辈修改申请已提交，等待树拥有者或树管理员审核"
            }));
        }

        [HttpDelete("Del/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var poem = await _poemService.GetByIdAsync(id) ?? throw new KeyNotFoundException("字辈不存在");
            var tree = await _genoTreeService.GetByIdAsync(poem.TreeID) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = GetCurrentUserId();
            var access = await _treePermissionService.GetTreeAccessAsync(tree.TreeID, currentUserId);

            if (!access.CanView)
            {
                throw new UnauthorizedAccessException("无权访问该家谱树");
            }

            if (access.CanDirectEdit)
            {
                var success = await _poemService.DeleteAsync(id, currentUserId);
                if (!success)
                {
                    throw new Exception("字辈删除失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "字辈删除成功"
                }));
            }

            if (!access.CanSubmitChange)
            {
                throw new UnauthorizedAccessException("当前身份不允许提交字辈删除申请");
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
                Reason = "修谱员提交字辈删除申请"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "字辈删除申请已提交，等待树拥有者或树管理员审核"
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
    }
}
