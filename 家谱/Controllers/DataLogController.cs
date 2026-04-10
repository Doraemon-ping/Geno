using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Services;

namespace 家谱.Controllers
{
    /// <summary>
    /// 数据库日志控制器。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DataLogController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;

        public DataLogController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// 分页查询数据库日志，支持按表名、操作类型、操作人和时间范围筛选。
        /// </summary>
        [HttpGet("query")]
        public async Task<IActionResult> Query([FromQuery] DataLogQueryDto query)
        {
            var userId = GetCurrentUserId();
            var result = await _auditLogService.QueryLogsAsync(userId, query);
            return Ok(ApiResponse.OK(result));
        }

        /// <summary>
        /// 兼容旧版日志列表接口。
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] int take = 100)
        {
            var userId = GetCurrentUserId();
            var logs = await _auditLogService.GetLogsAsync(userId, take);
            return Ok(ApiResponse.OK(logs));
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
    }
}
