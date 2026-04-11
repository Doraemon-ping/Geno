using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    /// 历史事件控制器。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EventController : ControllerBase
    {
        private readonly GenealogyDbContext _db;
        private readonly IGenoEventService _eventService;
        private readonly IGenoTreeService _treeService;
        private readonly IReviewService _reviewService;
        private readonly ITreePermissionService _treePermissionService;
        private readonly IMediaFileService _mediaFileService;

        public EventController(
            GenealogyDbContext db,
            IGenoEventService eventService,
            IGenoTreeService treeService,
            IReviewService reviewService,
            ITreePermissionService treePermissionService,
            IMediaFileService mediaFileService)
        {
            _db = db;
            _eventService = eventService;
            _treeService = treeService;
            _reviewService = reviewService;
            _treePermissionService = treePermissionService;
            _mediaFileService = mediaFileService;
        }

        /// <summary>
        /// 获取某棵树下的历史事件。
        /// </summary>
        [AllowAnonymous]
        [HttpGet("tree/{treeId}")]
        public async Task<IActionResult> GetTreeEvents(Guid treeId, [FromQuery] bool includeGlobal = true)
        {
            var tree = await _treeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("家谱树不存在");
            var currentUserId = TryGetCurrentUserId();

            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问该家谱树");
            }

            var access = currentUserId.HasValue
                ? await _treePermissionService.GetTreeAccessAsync(treeId, currentUserId.Value)
                : null;
            var includePrivateFamily = access?.IsTreeMember == true;
            var includePrivateGlobal = access?.IsSuperAdmin == true || access?.IsSystemAdmin == true;
            var events = await _eventService.GetByTreeIdAsync(treeId, includeGlobal, includePrivateFamily, includePrivateGlobal);
            return Ok(ApiResponse.OK(events));
        }

        /// <summary>
        /// 获取社会历史大事。
        /// </summary>
        [AllowAnonymous]
        [HttpGet("global")]
        public async Task<IActionResult> GetGlobalEvents()
        {
            var includePrivate = await IsSystemEventManagerAsync(TryGetCurrentUserId());
            var events = await _eventService.GetGlobalAsync(includePrivate);
            return Ok(ApiResponse.OK(events));
        }

        /// <summary>
        /// 获取事件详情。
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var entity = await _db.GenoEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.EventID == id)
                ?? throw new KeyNotFoundException("历史事件不存在");

            var currentUserId = TryGetCurrentUserId();
            if (!entity.IsPublic && !await CanViewPrivateEventAsync(entity, currentUserId))
            {
                throw new UnauthorizedAccessException("无权访问该未公开历史事件");
            }

            if (!entity.IsGlobal)
            {
                var treeId = entity.TreeID ?? throw new InvalidOperationException("树内历史事件缺少所属家谱树");
                var tree = await _treeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("家谱树不存在");

                if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
                {
                    throw new UnauthorizedAccessException("无权访问该历史事件");
                }
            }

            var result = await _eventService.GetByIdAsync(id) ?? throw new KeyNotFoundException("历史事件不存在");
            return Ok(ApiResponse.OK(result));
        }

        /// <summary>
        /// 上传事件附件。
        /// </summary>
        [Authorize]
        [HttpPost("media/upload")]
        [RequestSizeLimit(300 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 300 * 1024 * 1024)]
        public async Task<IActionResult> UploadMedia(
            [FromForm] IFormFile file,
            [FromForm] Guid? treeId,
            [FromForm] bool isGlobal = false,
            [FromForm] string? caption = null,
            [FromForm] int sortOrder = 1)
        {
            var currentUserId = GetCurrentUserId();
            await EnsureCanManageEventScopeAsync(currentUserId, treeId, isGlobal, allowSubmit: true);

            var result = await _mediaFileService.SaveTempAsync(
                file,
                currentUserId,
                isGlobal ? null : NormalizeTreeId(treeId),
                caption,
                sortOrder);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                AppliedDirectly = true,
                Message = "附件上传成功",
                Data = result
            }));
        }

        /// <summary>
        /// 新增历史事件。
        /// </summary>
        [Authorize]
        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] GenoEventDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var normalizedTreeId = dto.IsGlobal ? null : NormalizeTreeId(dto.TreeId);

            await _mediaFileService.EnsureMediaEditableAsync(dto.MediaIds, currentUserId, normalizedTreeId);
            var access = await EnsureCanManageEventScopeAsync(currentUserId, normalizedTreeId, dto.IsGlobal, allowSubmit: true);

            if (access.direct)
            {
                var entity = await _eventService.CreateAsync(dto, currentUserId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "历史事件创建成功",
                    Data = new { eventId = entity.EventID }
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = normalizedTreeId,
                ActionCode = ReviewActions.EventCreate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = dto.IsGlobal ? "提交新增社会历史事件申请" : "提交新增历史事件申请",
                ForceCreateTask = true
            }, currentUserId);

            await _mediaFileService.MarkPendingAsync(dto.MediaIds, currentUserId, taskId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = dto.IsGlobal ? "已提交社会历史事件申请，等待系统管理员或超级管理员审核" : "已提交历史事件申请，等待树拥有者或树管理员审核"
            }));
        }

        /// <summary>
        /// 修改历史事件。
        /// </summary>
        [Authorize]
        [HttpPut("Update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] GenoEventDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var existing = await _db.GenoEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.EventID == id)
                ?? throw new KeyNotFoundException("历史事件不存在");

            var normalizedTreeId = dto.IsGlobal ? null : NormalizeTreeId(dto.TreeId);
            if (existing.IsGlobal != dto.IsGlobal)
            {
                throw new InvalidOperationException("暂不支持直接在全局事件和树内事件之间切换");
            }

            await _mediaFileService.EnsureMediaEditableAsync(dto.MediaIds, currentUserId, normalizedTreeId, id);
            var access = await EnsureCanManageEventScopeAsync(currentUserId, existing.TreeID, existing.IsGlobal, allowSubmit: true);

            if (access.direct)
            {
                var success = await _eventService.UpdateAsync(id, dto, currentUserId);
                if (!success)
                {
                    throw new InvalidOperationException("历史事件更新失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "历史事件修改成功"
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = existing.TreeID,
                TargetId = id,
                ActionCode = ReviewActions.EventUpdate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = existing.IsGlobal ? "提交修改社会历史事件申请" : "提交修改历史事件申请"
            }, currentUserId);

            await _mediaFileService.MarkPendingAsync(dto.MediaIds, currentUserId, taskId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = existing.IsGlobal ? "已提交社会历史事件修改申请，等待系统管理员或超级管理员审核" : "已提交历史事件修改申请，等待树拥有者或树管理员审核"
            }));
        }

        /// <summary>
        /// 删除历史事件。
        /// </summary>
        [Authorize]
        [HttpDelete("Del/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            var existing = await _db.GenoEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.EventID == id)
                ?? throw new KeyNotFoundException("历史事件不存在");

            var access = await EnsureCanManageEventScopeAsync(currentUserId, existing.TreeID, existing.IsGlobal, allowSubmit: true);
            if (access.direct)
            {
                var success = await _eventService.DeleteAsync(id, currentUserId);
                if (!success)
                {
                    throw new InvalidOperationException("历史事件删除失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "历史事件删除成功"
                }));
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = existing.TreeID,
                TargetId = id,
                ActionCode = ReviewActions.EventDelete,
                ChangeData = JsonSerializer.Serialize(new
                {
                    existing.EventID,
                    existing.TreeID,
                    existing.EventTitle,
                    existing.EventType,
                    existing.IsGlobal,
                    existing.IsPublic,
                    existing.EventDate,
                    existing.DateRaw,
                    existing.LocationID,
                    existing.Description
                }, JsonDefaults.Options),
                Reason = existing.IsGlobal ? "提交删除社会历史事件申请" : "提交删除历史事件申请"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = existing.IsGlobal ? "已提交社会历史事件删除申请，等待系统管理员或超级管理员审核" : "已提交历史事件删除申请，等待树拥有者或树管理员审核"
            }));
        }

        private async Task<(bool direct, bool canSubmit)> EnsureCanManageEventScopeAsync(Guid userId, Guid? treeId, bool isGlobal, bool allowSubmit)
        {
            if (isGlobal)
            {
                var user = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.UserID == userId && item.UserStatus == 1)
                    ?? throw new UnauthorizedAccessException("当前用户不存在或已被禁用");

                var direct = user.RoleType is (byte)RoleType.SuperAdmin or (byte)RoleType.Admin;
                if (!direct && !allowSubmit)
                {
                    throw new UnauthorizedAccessException("无权管理社会历史事件");
                }

                return (direct, allowSubmit);
            }

            var normalizedTreeId = treeId ?? throw new InvalidOperationException("树内历史事件必须指定所属家谱树");
            var access = await _treePermissionService.GetTreeAccessAsync(normalizedTreeId, userId);
            if (!access.CanView)
            {
                throw new UnauthorizedAccessException("无权访问该家谱树");
            }

            if (!access.CanDirectEdit && !allowSubmit && !access.CanSubmitChange)
            {
                throw new UnauthorizedAccessException("当前身份无权管理历史事件");
            }

            if (!access.CanDirectEdit && allowSubmit && !access.CanSubmitChange)
            {
                throw new UnauthorizedAccessException("当前身份无权提交历史事件变更申请");
            }

            return (access.CanDirectEdit, access.CanSubmitChange);
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

        private async Task<bool> CanViewPrivateEventAsync(家谱.Models.Entities.GenoEvent entity, Guid? userId)
        {
            if (!userId.HasValue)
            {
                return false;
            }

            if (await IsSystemEventManagerAsync(userId))
            {
                return true;
            }

            if (!entity.IsGlobal && entity.TreeID.HasValue)
            {
                var access = await _treePermissionService.GetTreeAccessAsync(entity.TreeID.Value, userId.Value);
                return access.IsTreeMember;
            }

            return false;
        }

        private async Task<bool> IsSystemEventManagerAsync(Guid? userId)
        {
            if (!userId.HasValue)
            {
                return false;
            }

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserID == userId.Value && item.UserStatus == 1);

            return user?.RoleType is (byte)RoleType.SuperAdmin or (byte)RoleType.Admin;
        }

        private static Guid? NormalizeTreeId(Guid? treeId)
        {
            return treeId == Guid.Empty ? null : treeId;
        }
    }
}
