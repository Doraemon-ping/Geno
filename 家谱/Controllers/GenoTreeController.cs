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
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GenoTreeController : ControllerBase
    {
        private readonly IGenoTreeService _treeService;
        private readonly IReviewService _reviewService;
        private readonly ITreePermissionService _treePermissionService;

        public GenoTreeController(
            IGenoTreeService treeService,
            IReviewService reviewService,
            ITreePermissionService treePermissionService)
        {
            _treeService = treeService;
            _reviewService = reviewService;
            _treePermissionService = treePermissionService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
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

        [HttpPost("Add")]
        public async Task<IActionResult> Create([FromBody] GenoTreeDtos dto)
        {
            var userId = GetCurrentUserId();
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "请求体不能为空");

            dto.Owner = userId;
            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin)
            {
                var tree = await _treeService.CreateAsync(dto, userId, userId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "家谱树创建成功",
                    Data = new { treeId = tree.TreeID }
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                ActionCode = ReviewActions.TreeCreate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "普通用户提交新建家谱树申请",
                ForceCreateTask = true
            }, userId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "已提交创建申请，等待审核"
            }));
        }

        [HttpGet("my-trees")]
        public async Task<IActionResult> GetMyTrees()
        {
            var userId = GetCurrentUserId();
            var trees = await _treeService.GetAccessibleTreesAsync(userId);
            var result = new List<object>();
            foreach (var tree in trees)
            {
                var access = await _treePermissionService.GetTreeAccessAsync(tree.TreeID, userId);
                result.Add(new
                {
                    tree.TreeID,
                    tree.TreeName,
                    tree.AncestorName,
                    tree.Region,
                    tree.Description,
                    tree.OwnerID,
                    tree.IsPublic,
                    tree.CreateTime,
                    tree.IsDel,
                    Poems = tree.Poems,
                    Access = access
                });
            }

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllPublic()
        {
            var trees = await _treeService.GetAll();
            return Ok(trees.Where(t => t.IsPublic));
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var tree = await _treeService.GetByIdAsync(id);
            if (tree == null) return NotFound(new { message = "未找到家族树" });

            var currentUserId = GetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                return Forbid();
            }

            var access = await _treePermissionService.GetTreeAccessAsync(id, currentUserId);
            var permissions = access.CanManagePermissions
                ? await _treePermissionService.GetPermissionsAsync(id)
                : new List<TreePermissionDto>();

            return Ok(new
            {
                tree.TreeID,
                tree.TreeName,
                tree.AncestorName,
                tree.Region,
                tree.Description,
                tree.OwnerID,
                tree.IsPublic,
                tree.CreateTime,
                Poems = tree.Poems,
                Access = access,
                Permissions = permissions
            });
        }

        [HttpPut("Update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] GenoTreeDtos dto)
        {
            var existingTree = await _treeService.GetByIdAsync(id);
            if (existingTree == null) return NotFound();

            var userId = GetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(existingTree, userId))
            {
                return Forbid();
            }

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(id, userId))
            {
                var success = await _treeService.UpdateAsync(dto, id, userId);
                return success
                    ? Ok(ApiResponse.OK(new WorkflowResultDto { AppliedDirectly = true, Message = "更新成功" }))
                    : BadRequest();
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = id,
                TargetId = id,
                ActionCode = ReviewActions.TreeUpdate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "普通用户提交家谱树修改申请"
            }, userId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "修改申请已提交，等待审核"
            }));
        }

        [HttpDelete("Del/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existingTree = await _treeService.GetByIdAsync(id);
            if (existingTree == null) return NotFound();

            var userId = GetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(existingTree, userId))
            {
                return Forbid();
            }

            if (GetCurrentUserRole() == (byte)RoleType.SuperAdmin || await _treePermissionService.CanEditTreeAsync(id, userId))
            {
                var success = await _treeService.DeleteAsync(id, userId);
                return success
                    ? Ok(ApiResponse.OK(new WorkflowResultDto { AppliedDirectly = true, Message = "删除成功" }))
                    : BadRequest();
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = id,
                TargetId = id,
                ActionCode = ReviewActions.TreeDelete,
                ChangeData = JsonSerializer.Serialize(new
                {
                    existingTree.TreeID,
                    existingTree.TreeName,
                    existingTree.AncestorName,
                    existingTree.Region,
                    existingTree.Description,
                    existingTree.IsPublic
                }, JsonDefaults.Options),
                Reason = "普通用户提交家谱树删除申请"
            }, userId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "删除申请已提交，等待审核"
            }));
        }
    }
}
