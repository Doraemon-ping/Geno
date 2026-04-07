namespace 家谱.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity.Data;
    using Microsoft.AspNetCore.Mvc;
    using System.Runtime.InteropServices;
    using System.Security.Claims;
    using System.Text.Json;
    using 家谱.Models.DTOs;
    using 家谱.Models.DTOs.Common;
    using 家谱.Models.Enums;
    using 家谱.Services;

    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IReviewService _reviewService;

        public AccountController(IAuthService authService, IReviewService reviewService)
        {
            _authService = authService;
            _reviewService = reviewService;
        }

        [HttpPost("send-code")]
        public async Task<IActionResult> SendCode([FromBody] string email)
        {
            var result = await _authService.SendEmailCodeAsync(email);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(new { msg = "验证码已发送，请检查邮箱" });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto request, [FromQuery] string code)
        {
            var result = await _authService.RegisterAsync(request, code);
            if (result.Success) return Ok(new { message = result.Message });
            return BadRequest(new { message = result.Message });
        }

        /// 用户登录，成功后返回 JWT 令牌

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto request)
        {
            var result = await _authService.LoginAsync(request);

            if (result.Success)
            {
                return Ok(new
                {
                    token = result.Token,
                    message = result.Message
                });
            }

            return Unauthorized(new { message = result.Message });
        }

        // 受保护的测试接口，只有认证成功的用户才能访问

        [Authorize] // 核心：强制开启 JWT 验证
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            // 如果还没进到这一步就 401 了，说明是 Program.cs 里的 AddJwtBearer 配置有问题
            if (!User.Identity.IsAuthenticated)
            {
                System.Console.WriteLine("{ debug = 虽然进了方法，但 Identity 依然显示未认证 }");
            }

            // 1. 从 Token 解析出当前登录用户的 ID
            // 尝试直接读取 "sub"，这是 JWT 协议中用户 ID 的标准名称
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                // 如果还是拿不到，我们打印出所有的 Claims 看看它们到底叫什么
                var allClaims = string.Join(", ", User.Claims.Select(c => $"{c.Type}:{c.Value}"));
                return Unauthorized(new { message = $"找不到身份信息。当前 Claims: {allClaims}" });
            }

            // 2. 调用服务获取详细资料
            var profile = await _authService.GetUserProfileAsync(userId);

            if (profile == null)
            {
                return NotFound(new { message = "未找到该成员信息" });
            }

            return Ok(profile);
        }

        //资料更新接口，要求用户必须登录才能访问

        [Authorize]
        [HttpPut("update-profile")] // 使用 PUT 表示更新资源
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            // 1. 解析当前登录者的 ID (依然是从 Token 拿，保证安全)
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized(new { message = "身份失效" });
            }

            // 2. 执行更新
            var success = await _authService.UpdateUserProfileAsync(userId, dto);

            if (!success.Item1)
            {
                return BadRequest(new { message = "更新失败，用户不存在或未做任何更改" });
            }

            return Ok(new { message = "资料更新成功！" });
        }

        [HttpPost("forgot-password-code")]
        public async Task<IActionResult> ForgotPasswordCode([FromBody] string email)
        {
            var result = await _authService.SendResetPasswordCodeAsync(email);
            return result.success ? Ok(new { message = result.message }) : BadRequest(new { message = result.message });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            var result = await _authService.ResetPasswordAsync(request.Email, request.Code, request.NewPassword);
            return result.success ? Ok(new { message = result.message }) : BadRequest(new { message = result.message });
        }

        // 申请管理员权限接口，用户提交后进入审核流程
        [HttpPost("apply-admin")]
        [Authorize] // 必须登录
        public async Task<IActionResult> ApplyAdmin([FromBody] RoleApplyPayload dto)
        {
            // 1. 获取当前登录用户 ID (假设从 Claims 中取)
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            // 2. 验证目标用户是否存在（如果 TargetId 是用户 ID）
            var user = await _authService.GetUserProfileAsync(dto.TargetId);
            if (user == null)
            {
                return NotFound(new { message = "目标用户不存在" });
            }

            var submitResult = new SubmitReviewRequest
            {
                TreeId = null, // 升限不针对特定家谱树
                TargetId = dto.TargetId, // 申请的目标 ID
                ActionCode = ReviewActions.ApplyAdmin, // 预定义的操作代码
                ChangeData = JsonSerializer.Serialize(dto),
                Reason = dto.Reason
            };
            var result = await _reviewService.SubmitAsync(submitResult, userId);
            return Ok(ApiResponse.Ok("申请已提交"));
        }
    }
}
