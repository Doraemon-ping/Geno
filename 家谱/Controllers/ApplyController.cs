using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using 家谱.Common;
using 家谱.DB;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Enums;
using 家谱.Services;

namespace 家谱.Controllers
{
    [Authorize]// 核心：要求用户必须登录才能访问这个控制器的任何方法
    [ApiController]
    [Route("api/[controller]")]
    public class ApplyController : ControllerBase
    {
        private IAuthService _authService;
        private IReviewService _reviewService;
      

        public ApplyController(IAuthService authService,
                               IReviewService reviewService
                              )
        {
            _authService = authService;
            _reviewService = reviewService;
          
        }

        // 这里可以添加与审核相关的接口，例如提交审核任务、查询审核状态等

        // 申请管理员权限接口，用户提交后进入审核流程
        [HttpPost("apply-admin")]
        [Authorize] // 必须登录
        public async Task<IActionResult> ApplyAdmin([FromBody] RoleApplyPayload dto)
        {
            // 1. 获取当前登录用户 ID (假设从 Claims 中取)
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            //只验证dto，业务逻辑审核在服务层进行
            if (dto.NewRole == 0)
                throw new ArgumentException("不允许申请!");
            if (dto.NewRole != 1 && dto.NewRole != 2)
                throw new Exception("只能申请有效权限");
            if (dto.TargetId.IsNullOrEmpty())
                throw new Exception("目标修改用户不能为空");
            //统一接口，业务层无法区别验证，提交后由审核流程决定是否通过
            Guid targetGuid = Guid.Parse(dto.TargetId);
            var submitResult = new SubmitReviewRequest
            {
                TreeId = null, // 升限不针对特定家谱树
                TargetId = targetGuid, // 申请的目标 ID
                ActionCode = ReviewActions.ApplyAdmin, // 预定义的操作代码
                ChangeData = JsonSerializer.Serialize(dto, JsonDefaults.Options),
                Reason = dto.Reason
            };
            var result = await _reviewService.SubmitAsync(submitResult, userId);
            return Ok(ApiResponse.OK());
        }
    }
}