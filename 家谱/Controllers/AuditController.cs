namespace 家谱.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Identity.Client;
    using System.Security.Claims;
    using 家谱.Models.DTOs;
    using 家谱.Models.DTOs.Common;
    using 家谱.Services;

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly IAuditService _auditService;
        private readonly DB.GenealogyDbContext _db;

        public AuditController(IAuditService auditService, DB.GenealogyDbContext context)
        {
            _auditService = auditService;
            _db = context;
        }

        // 用户：申请升级权限

        [HttpPost("apply-role")]
        public async Task<ApiResponse> ApplyRole([FromBody] ApplyRoleRequest dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return ApiResponse.Fail("用户不存在");
            if (user.RoleType == dto.TargetRole) return ApiResponse.Fail("你已经拥有该权限了");

            var result = await _auditService.SubmitRoleUpgradeAsync(userId, dto);
            return result.success ? Ok(new { message = result.message }) : BadRequest(new { message = result.message });
        }

        // 管理员：查看待审核列表 (要求 RoleType <= 2)

        [HttpGet("pending-list")]
        public async Task<IActionResult> GetPendingList()
        {
            // 这里可以根据 User.RoleType 判断是否为管理员
            if (User == null || !User.Identity.IsAuthenticated || byte.Parse(User.FindFirst(ClaimTypes.Role)!.Value) > 2)
                throw new UnauthorizedAccessException("无权限访问此资源");
            var list = await _auditService.GetPendingAuditsAsync();
            return Ok(list);
        }

        // 管理员：处理申请

        [HttpPost("handle")]
        public async Task<IActionResult> Handle([FromBody] HandleAuditDto dto)
        {
            if (User == null || !User.Identity.IsAuthenticated || byte.Parse(User.FindFirst(ClaimTypes.Role)!.Value) > 2)
                throw new UnauthorizedAccessException("无权限访问此资源");
            var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _auditService.HandleAuditAsync(adminId, dto);
            return result.success ? Ok(new { message = result.message }) : BadRequest(new { message = result.message });
        }
    }
}
