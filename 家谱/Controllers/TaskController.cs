using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Services;

namespace 家谱.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TaskController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public TaskController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [HttpGet("my-tasks")]
        public async Task<ActionResult> GetTasks()
        {
            var userId = GetCurrentUserId();
            var tasks = await _reviewService.GetTaskList(userId);
            return Ok(ApiResponse.OK(tasks));
        }

        [HttpPost("get-all")]
        public async Task<ActionResult> GetAllTasks()
        {
            var userId = GetCurrentUserId();
            var tasks = await _reviewService.GetAll(userId);
            return Ok(ApiResponse.OK(tasks));
        }

        [HttpPost("process")]
        public async Task<ActionResult> ProcessTask([FromBody] TaskProcessDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _reviewService.ProcessAsync(dto, userId);
            return Ok(ApiResponse.OK(result));
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("无法解析当前用户身份");

            return userId;
        }
    }
}
