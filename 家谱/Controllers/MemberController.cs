using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    public class MemberController : ControllerBase
    {
        private readonly IGenoMemberService _memberService;
        private readonly IGenoTreeService _treeService;
        private readonly IReviewService _reviewService;
        private readonly ITreePermissionService _treePermissionService;
        private readonly IMediaFileService _mediaFileService;

        public MemberController(
            IGenoMemberService memberService,
            IGenoTreeService treeService,
            IReviewService reviewService,
            ITreePermissionService treePermissionService,
            IMediaFileService mediaFileService)
        {
            _memberService = memberService;
            _treeService = treeService;
            _reviewService = reviewService;
            _treePermissionService = treePermissionService;
            _mediaFileService = mediaFileService;
        }

        [AllowAnonymous]
        [HttpGet("tree/{treeId}")]
        public async Task<IActionResult> GetList(Guid treeId)
        {
            var tree = await _treeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("瀹惰氨鏍戜笉瀛樺湪");
            var currentUserId = TryGetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("鏃犳潈璁块棶璇ュ璋辨爲");
            }

            var members = await _memberService.GetByTreeIdAsync(treeId);
            return Ok(ApiResponse.OK(members));
        }

        [AllowAnonymous]
        [HttpGet("query")]
        public async Task<IActionResult> Query([FromQuery] MemberQueryDto dto)
        {
            if (dto.TreeId == Guid.Empty)
            {
                throw new ArgumentException("家谱树不能为空");
            }

            var tree = await _treeService.GetByIdAsync(dto.TreeId) ?? throw new KeyNotFoundException("瀹惰氨鏍戜笉瀛樺湪");
            var currentUserId = TryGetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("鏃犳潈璁块棶璇ュ璋辨爲");
            }

            var result = await _memberService.QueryAsync(dto);
            return Ok(ApiResponse.OK(result));
        }

        [AllowAnonymous]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var member = await _memberService.GetByIdAsync(id) ?? throw new KeyNotFoundException("鏍戞垚鍛樹笉瀛樺湪");
            var tree = await _treeService.GetByIdAsync(member.TreeID) ?? throw new KeyNotFoundException("瀹惰氨鏍戜笉瀛樺湪");
            var currentUserId = TryGetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("鏃犳潈璁块棶璇ュ璋辨爲");
            }

            return Ok(ApiResponse.OK(member));
        }

        [AllowAnonymous]
        [HttpGet("media/{id}")]
        public async Task<IActionResult> GetMedia(Guid id)
        {
            var member = await _memberService.GetByIdAsync(id) ?? throw new KeyNotFoundException("鏍戞垚鍛樹笉瀛樺湪");
            var tree = await _treeService.GetByIdAsync(member.TreeID) ?? throw new KeyNotFoundException("瀹惰氨鏍戜笉瀛樺湪");
            var currentUserId = TryGetCurrentUserId();
            if (!await _treePermissionService.CanViewTreeAsync(tree, currentUserId))
            {
                throw new UnauthorizedAccessException("鏃犳潈璁块棶璇ュ璋辨爲");
            }

            var mediaFiles = await _mediaFileService.GetByOwnerAsync("member", id);
            return Ok(ApiResponse.OK(mediaFiles));
        }

        [HttpPost("media/upload")]
        [RequestSizeLimit(300 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 300 * 1024 * 1024)]
        public async Task<IActionResult> UploadMedia(
            [FromForm] IFormFile file,
            [FromForm] Guid treeId,
            [FromForm] string? caption = null,
            [FromForm] int sortOrder = 1)
        {
            var tree = await _treeService.GetByIdAsync(treeId) ?? throw new KeyNotFoundException("瀹惰氨鏍戜笉瀛樺湪");
            var currentUserId = GetCurrentUserId();
            var access = await _treePermissionService.GetTreeAccessAsync(tree.TreeID, currentUserId);
            if (!access.CanView)
            {
                throw new UnauthorizedAccessException("鏃犳潈璁块棶璇ュ璋辨爲");
            }

            if (!access.CanDirectEdit && !access.CanSubmitChange)
            {
                throw new UnauthorizedAccessException("当前身份不允许上传成员资料");
            }

            var mediaFile = await _mediaFileService.SaveTempAsync(file, currentUserId, treeId, "member", caption, sortOrder);
            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                AppliedDirectly = true,
                Message = "鎴愬憳璧勬枡涓婁紶鎴愬姛",
                Data = mediaFile
            }));
        }

        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] GenoMemberDto dto)
        {
            var tree = await _treeService.GetByIdAsync(dto.TreeId) ?? throw new KeyNotFoundException("瀹惰氨鏍戜笉瀛樺湪");
            var currentUserId = GetCurrentUserId();
            var access = await _treePermissionService.GetTreeAccessAsync(tree.TreeID, currentUserId);
            await _mediaFileService.EnsureMediaEditableAsync(dto.MediaIds, currentUserId, dto.TreeId, ownerType: "member");

            if (!access.CanView)
            {
                throw new UnauthorizedAccessException("鏃犳潈璁块棶璇ュ璋辨爲");
            }

            if (access.CanDirectEdit)
            {
                var member = await _memberService.CreateAsync(dto, currentUserId);
                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "树成员添加成功",
                    Data = new { memberId = member.MemberID }
                }));
            }

            if (!access.CanSubmitChange)
            {
                throw new UnauthorizedAccessException("褰撳墠韬唤涓嶅厑璁告彁浜ゆ爲鎴愬憳鍙樻洿鐢宠");
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                ActionCode = ReviewActions.MemberCreate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "淇氨鍛樻彁浜ゆ柊澧炴爲鎴愬憳鐢宠",
                ForceCreateTask = true
            }, currentUserId);

            await _mediaFileService.MarkPendingAsync(dto.MediaIds, currentUserId, taskId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "鏂板鎴愬憳鐢宠宸叉彁浜わ紝绛夊緟鏍戞嫢鏈夎€呮垨鏍戠鐞嗗憳瀹℃牳"
            }));
        }

        [HttpPut("Update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] GenoMemberDto dto)
        {
            var member = await _memberService.GetByIdAsync(id) ?? throw new KeyNotFoundException("鏍戞垚鍛樹笉瀛樺湪");
            if (member.TreeID != dto.TreeId)
            {
                throw new ArgumentException("不允许跨家谱树修改成员");
            }

            var tree = await _treeService.GetByIdAsync(dto.TreeId) ?? throw new KeyNotFoundException("瀹惰氨鏍戜笉瀛樺湪");
            var currentUserId = GetCurrentUserId();
            var access = await _treePermissionService.GetTreeAccessAsync(tree.TreeID, currentUserId);
            await _mediaFileService.EnsureMediaEditableAsync(dto.MediaIds, currentUserId, dto.TreeId, id, "member");

            if (!access.CanView)
            {
                throw new UnauthorizedAccessException("鏃犳潈璁块棶璇ュ璋辨爲");
            }

            if (access.CanDirectEdit)
            {
                var success = await _memberService.UpdateAsync(id, dto, currentUserId);
                if (!success)
                {
                    throw new InvalidOperationException("树成员更新失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "鏍戞垚鍛樺凡鏇存柊"
                }));
            }

            if (!access.CanSubmitChange)
            {
                throw new UnauthorizedAccessException("褰撳墠韬唤涓嶅厑璁告彁浜ゆ爲鎴愬憳淇敼鐢宠");
            }

            var taskId = await _reviewService.SubmitAsync(new SubmitReviewRequest
            {
                TreeId = dto.TreeId,
                TargetId = id,
                ActionCode = ReviewActions.MemberUpdate,
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = "淇氨鍛樻彁浜ゆ爲鎴愬憳淇敼鐢宠"
            }, currentUserId);

            await _mediaFileService.MarkPendingAsync(dto.MediaIds, currentUserId, taskId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "鎴愬憳淇敼鐢宠宸叉彁浜わ紝绛夊緟鏍戞嫢鏈夎€呮垨鏍戠鐞嗗憳瀹℃牳"
            }));
        }

        [HttpPut("Media/{id}")]
        public async Task<IActionResult> UpdateMedia(Guid id, [FromBody] MemberMediaUpdateDto dto)
        {
            var member = await _memberService.GetByIdAsync(id) ?? throw new KeyNotFoundException("鏍戞垚鍛樹笉瀛樺湪");
            var fullDto = new GenoMemberDto
            {
                TreeId = member.TreeID,
                FirstName = member.FirstName,
                LastName = member.LastName,
                GenerationNum = member.GenerationNum,
                PoemId = member.PoemID,
                Gender = member.Gender,
                BirthDate = member.BirthDate,
                BirthDateRaw = member.BirthDateRaw,
                DeathDate = member.DeathDate,
                IsLiving = member.IsLiving,
                Biography = member.Biography,
                SysUserId = member.SysUserId,
                MediaIds = dto.MediaIds
            };

            return await Update(id, fullDto);
        }

        [HttpDelete("Del/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var member = await _memberService.GetByIdAsync(id) ?? throw new KeyNotFoundException("鏍戞垚鍛樹笉瀛樺湪");
            var tree = await _treeService.GetByIdAsync(member.TreeID) ?? throw new KeyNotFoundException("瀹惰氨鏍戜笉瀛樺湪");
            var currentUserId = GetCurrentUserId();
            var access = await _treePermissionService.GetTreeAccessAsync(tree.TreeID, currentUserId);

            if (!access.CanView)
            {
                throw new UnauthorizedAccessException("鏃犳潈璁块棶璇ュ璋辨爲");
            }

            if (access.CanDirectEdit)
            {
                var success = await _memberService.DeleteAsync(id, currentUserId);
                if (!success)
                {
                    throw new InvalidOperationException("树成员删除失败");
                }

                return Ok(ApiResponse.OK(new WorkflowResultDto
                {
                    AppliedDirectly = true,
                    Message = "鏍戞垚鍛樺凡鍒犻櫎"
                }));
            }

            if (!access.CanSubmitChange)
            {
                throw new UnauthorizedAccessException("褰撳墠韬唤涓嶅厑璁告彁浜ゆ爲鎴愬憳鍒犻櫎鐢宠");
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
                Reason = "淇氨鍛樻彁浜ゆ爲鎴愬憳鍒犻櫎鐢宠"
            }, currentUserId);

            return Ok(ApiResponse.OK(new WorkflowResultDto
            {
                SubmittedForReview = true,
                TaskId = taskId,
                Message = "鎴愬憳鍒犻櫎鐢宠宸叉彁浜わ紝绛夊緟鏍戞嫢鏈夎€呮垨鏍戠鐞嗗憳瀹℃牳"
            }));
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("鏃犳硶瑙ｆ瀽褰撳墠鐢ㄦ埛韬唤");
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





