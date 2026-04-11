using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
    public class CommentController : ControllerBase
    {
        private readonly GenealogyDbContext _db;
        private readonly ICommentService _commentService;
        private readonly IGenoTreeService _treeService;
        private readonly ITreePermissionService _treePermissionService;

        public CommentController(
            GenealogyDbContext db,
            ICommentService commentService,
            IGenoTreeService treeService,
            ITreePermissionService treePermissionService)
        {
            _db = db;
            _commentService = commentService;
            _treeService = treeService;
            _treePermissionService = treePermissionService;
        }

        [AllowAnonymous]
        [HttpGet("owner/{ownerType}/{ownerId}")]
        public async Task<IActionResult> GetByOwner(string ownerType, Guid ownerId)
        {
            var treeId = await ResolveTreeIdAsync(ownerType, ownerId);
            if (treeId.HasValue)
            {
                await EnsureCanViewTreeAsync(treeId.Value);
            }

            var comments = await _commentService.GetByOwnerAsync(ownerType, ownerId);
            return Ok(ApiResponse.OK(comments));
        }

        [Authorize]
        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] CommentCreateDto dto)
        {
            var userId = GetCurrentUserId();
            var treeId = await ResolveTreeIdAsync(dto.OwnerType, dto.OwnerId) ?? (dto.TreeId == Guid.Empty ? null : dto.TreeId);
            if (treeId.HasValue)
            {
                await EnsureCanViewTreeAsync(treeId.Value);
            }

            dto.TreeId = treeId;
            var comment = await _commentService.CreateAsync(dto, userId);
            return Ok(ApiResponse.OK(comment));
        }

        [Authorize]
        [HttpDelete("Del/{commentId}")]
        public async Task<IActionResult> Delete(Guid commentId)
        {
            var userId = GetCurrentUserId();
            var comment = await _db.GenoComments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.CommentID == commentId)
                ?? throw new KeyNotFoundException("评论不存在");

            if (comment.UserID != userId)
            {
                var user = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.UserID == userId && item.UserStatus == 1);

                var isSuperAdmin = user?.RoleType == (byte)RoleType.SuperAdmin;
                var canManageTree = false;
                if (comment.TreeID.HasValue)
                {
                    var access = await _treePermissionService.GetTreeAccessAsync(comment.TreeID.Value, userId);
                    canManageTree = access.CanManagePermissions;
                }

                if (!isSuperAdmin && !canManageTree)
                {
                    throw new UnauthorizedAccessException("无权删除这条评论");
                }
            }

            var success = await _commentService.DeleteAsync(commentId, userId);
            if (!success)
            {
                throw new InvalidOperationException("评论删除失败");
            }

            return Ok(ApiResponse.OK(new { commentId }));
        }

        private async Task<Guid?> ResolveTreeIdAsync(string ownerType, Guid ownerId)
        {
            var normalizedOwnerType = string.IsNullOrWhiteSpace(ownerType) ? "event" : ownerType.Trim().ToLowerInvariant();
            if (normalizedOwnerType == "event")
            {
                var item = await _db.GenoEvents
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(entity => entity.EventID == ownerId && !entity.IsDel);
                return item?.TreeID;
            }

            if (normalizedOwnerType == "member")
            {
                var item = await _db.GenoMembers
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(entity => entity.MemberID == ownerId && entity.IsDel != true);
                return item?.TreeID;
            }

            if (normalizedOwnerType == "post")
            {
                var item = await _db.SpacePosts
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(entity => entity.PostID == ownerId && !entity.IsDel);
                return item?.TreeID;
            }

            if (normalizedOwnerType == "tree")
            {
                return ownerId;
            }

            return null;
        }

        private async Task EnsureCanViewTreeAsync(Guid treeId)
        {
            var tree = await _treeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = TryGetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问该家谱树");
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
