using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Enums;
using 家谱.Services;

namespace 家谱.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SpacePostController : ControllerBase
    {
        private readonly GenealogyDbContext _db;
        private readonly ISpacePostService _spacePostService;
        private readonly IGenoTreeService _treeService;
        private readonly ITreePermissionService _treePermissionService;
        private readonly IMediaFileService _mediaFileService;

        public SpacePostController(
            GenealogyDbContext db,
            ISpacePostService spacePostService,
            IGenoTreeService treeService,
            ITreePermissionService treePermissionService,
            IMediaFileService mediaFileService)
        {
            _db = db;
            _spacePostService = spacePostService;
            _treeService = treeService;
            _treePermissionService = treePermissionService;
            _mediaFileService = mediaFileService;
        }

        [AllowAnonymous]
        [HttpGet("tree/{treeId}")]
        public async Task<IActionResult> GetTreePosts(Guid treeId)
        {
            await EnsureCanViewTreeAsync(treeId);
            return Ok(ApiResponse.OK(await _spacePostService.GetByTreeAsync(treeId)));
        }

        [Authorize]
        [HttpPost("media/upload")]
        [RequestSizeLimit(300 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 300 * 1024 * 1024)]
        public async Task<IActionResult> UploadMedia(
            [FromForm] IFormFile file,
            [FromForm] Guid treeId,
            [FromForm] string? caption = null,
            [FromForm] int sortOrder = 1)
        {
            var currentUserId = GetCurrentUserId();
            await EnsureCanPostTreeAsync(treeId, currentUserId);
            var result = await _mediaFileService.SaveTempAsync(file, currentUserId, treeId, "post", caption, sortOrder);
            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                AppliedDirectly = true,
                Message = "帖子资料上传成功",
                Data = result
            }));
        }

        [Authorize]
        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] SpacePostCreateDto dto)
        {
            var currentUserId = GetCurrentUserId();
            await EnsureCanPostTreeAsync(dto.TreeId, currentUserId);
            await _mediaFileService.EnsureMediaEditableAsync(dto.MediaIds, currentUserId, dto.TreeId, ownerType: "post");
            return Ok(ApiResponse.OK(await _spacePostService.CreateAsync(dto, currentUserId)));
        }

        [Authorize]
        [HttpPut("Update/{postId}")]
        public async Task<IActionResult> Update(Guid postId, [FromBody] SpacePostUpdateDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var post = await _db.SpacePosts
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.PostID == postId)
                ?? throw new KeyNotFoundException("帖子不存在");

            if (post.UserID != currentUserId)
            {
                throw new UnauthorizedAccessException("只有发帖人可以修改这条帖子");
            }

            await EnsureCanPostTreeAsync(post.TreeID, currentUserId);
            await _mediaFileService.EnsureMediaEditableAsync(dto.MediaIds, currentUserId, post.TreeID, postId, "post");
            var updated = await _spacePostService.UpdateAsync(postId, dto, currentUserId)
                ?? throw new InvalidOperationException("帖子更新失败");

            return Ok(ApiResponse.OK(updated));
        }

        [Authorize]
        [HttpDelete("Del/{postId}")]
        public async Task<IActionResult> Delete(Guid postId)
        {
            var currentUserId = GetCurrentUserId();
            var post = await _db.SpacePosts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.PostID == postId)
                ?? throw new KeyNotFoundException("帖子不存在");

            if (post.UserID != currentUserId)
            {
                var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.UserID == currentUserId);
                var isSuperAdmin = user?.RoleType == (byte)RoleType.SuperAdmin;
                var access = await _treePermissionService.GetTreeAccessAsync(post.TreeID, currentUserId);
                if (!isSuperAdmin && !access.CanManagePermissions)
                {
                    throw new UnauthorizedAccessException("无权删除这条帖子");
                }
            }

            var success = await _spacePostService.DeleteAsync(postId, currentUserId);
            if (!success)
            {
                throw new InvalidOperationException("帖子删除失败");
            }

            return Ok(ApiResponse.OK(new { postId }));
        }

        private async Task EnsureCanViewTreeAsync(Guid treeId, Guid? userId = null)
        {
            var tree = await _treeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = userId ?? TryGetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问该家谱树");
            }
        }

        private async Task EnsureCanPostTreeAsync(Guid treeId, Guid userId)
        {
            await EnsureCanViewTreeAsync(treeId, userId);
            var access = await _treePermissionService.GetTreeAccessAsync(treeId, userId);
            var isLinkedMember = await _db.GenoMembers
                .AsNoTracking()
                .AnyAsync(item => item.TreeID == treeId && item.SysUserId == userId);

            if (!access.IsTreeMember && !isLinkedMember)
            {
                throw new UnauthorizedAccessException("只有该家谱树成员可以发布普通帖子");
            }
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
