using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Services;

namespace 家谱.Controllers
{
    /// <summary>
    /// 系统通知公告控制器。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AnnouncementController : ControllerBase
    {
        private readonly IAnnouncementService _announcementService;

        public AnnouncementController(IAnnouncementService announcementService)
        {
            _announcementService = announcementService;
        }

        [AllowAnonymous]
        [HttpGet("public")]
        public async Task<IActionResult> Public([FromQuery] AnnouncementQueryDto query)
        {
            query.PublicOnly = true;
            return Ok(ApiResponse.OK(await _announcementService.QueryAsync(TryGetCurrentUserId(), query)));
        }

        [Authorize]
        [HttpGet("query")]
        public async Task<IActionResult> Query([FromQuery] AnnouncementQueryDto query)
        {
            return Ok(ApiResponse.OK(await _announcementService.QueryAsync(GetCurrentUserId(), query)));
        }

        [Authorize]
        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] AnnouncementCreateDto dto)
        {
            return Ok(ApiResponse.OK(await _announcementService.CreateAsync(GetCurrentUserId(), dto)));
        }

        [Authorize]
        [HttpPut("Update/{announcementId}")]
        public async Task<IActionResult> Update(Guid announcementId, [FromBody] AnnouncementUpdateDto dto)
        {
            return Ok(ApiResponse.OK(await _announcementService.UpdateAsync(GetCurrentUserId(), announcementId, dto)));
        }

        [Authorize]
        [HttpDelete("Del/{announcementId}")]
        public async Task<IActionResult> Delete(Guid announcementId)
        {
            var success = await _announcementService.DeleteAsync(GetCurrentUserId(), announcementId);
            if (!success)
            {
                throw new KeyNotFoundException("公告不存在");
            }

            return Ok(ApiResponse.OK(new { announcementId }));
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
