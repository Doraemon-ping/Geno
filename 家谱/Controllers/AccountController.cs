namespace 家谱.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;
    using 家谱.Models.DTOs;
    using 家谱.Models.DTOs.Common;
    using 家谱.Services;

    /// <summary>
    /// Defines the <see cref="AccountController" />
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        /// <summary>
        /// Defines the _authService
        /// </summary>
        private readonly IAuthService _authService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountController"/> class.
        /// </summary>
        /// <param name="authService">The authService<see cref="IAuthService"/></param>
        public AccountController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// The SendCode
        /// </summary>
        /// <param name="email">The email<see cref="string"/></param>
        /// <returns>The <see cref="Task{IActionResult}"/></returns>
        [HttpPost("send-code")]
        public async Task<IActionResult> SendCode([FromBody] string email)
        {
            var result = await _authService.SendEmailCodeAsync(email);
            if (!result.Success)
                throw new Exception(result.Message); // 这里直接抛异常，交给全局异常处理中间件处理，返回统一格式的错误响应
            return Ok(ApiResponse.OK());
        }

        /// <summary>
        /// The Register
        /// </summary>
        /// <param name="request">The request<see cref="UserRegisterDto"/></param>
        /// <param name="code">The code<see cref="string"/></param>
        /// <returns>The <see cref="Task{IActionResult}"/></returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto request, [FromQuery] string code)
        {

            if (string.IsNullOrEmpty(request.Username))
            {
                throw new ArgumentException("用户名不能为空");
            }
            if (string.IsNullOrEmpty(request.Password))
            {
                throw new ArgumentException("密码不能为空");
            }
            if (string.IsNullOrEmpty(request.Email))
            {
                throw new ArgumentException("邮箱不能为空");
            }
            if (!request.Email.EndsWith(".com"))
            {
                throw new ArgumentException("请输入有效的邮箱地址");
            }
            if (string.IsNullOrEmpty(request.Phone))
            {
                throw new ArgumentException("手机号不能为空");

            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Phone, @"^\d{10,15}$"))
            {
                throw new ArgumentException("请输入有效的电话号码");
            }
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("验证码不能为空");
            }

            var result = await _authService.RegisterAsync(request, code);
            if (result.Success) return Ok(ApiResponse.OK());
            throw new Exception(result.Message); // 这里直接抛异常，交给全局异常处理中间件处理，返回统一格式的错误响应
        }

        /// 用户登录，成功后返回 JWT 令牌

        /// <summary>
        /// The Login
        /// </summary>
        /// <param name="request">The request<see cref="LoginDto"/></param>
        /// <returns>The <see cref="Task{IActionResult}"/></returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto request)
        {
            if (string.IsNullOrEmpty(request.Username))
            {
                throw new ArgumentException("用户名不能为空");
            }
            if (string.IsNullOrEmpty(request.Password))
            {
                throw new ArgumentException("密码不能为空");
            }

            var result = await _authService.LoginAsync(request);

            if (result.Success)
            {
                var data = new
                {
                    token = result.Token
                };
                return Ok(ApiResponse.OK(data));
            }
            throw new Exception(result.Message);
        }

        // 受保护的测试接口，只有认证成功的用户才能访问

        /// <summary>
        /// The GetProfile
        /// </summary>
        /// <returns>The <see cref="Task{IActionResult}"/></returns>
        [Authorize] // 核心：强制开启 JWT 验证
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            // 如果还没进到这一步就 401 了，说明是 Program.cs 里的 AddJwtBearer 配置有问题
            if (User.Identity?.IsAuthenticated != true)
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
                throw new Exception($"无法解析用户 ID，Claims 中的内容有：{allClaims}");
            }

            // 2. 调用服务获取详细资料
            var profile = await _authService.GetUserProfileAsync(userId);

            if (profile == null)
            {
                throw new Exception("用户资料不存在");
            }

            return Ok(ApiResponse.OK(profile));
        }

        //资料更新接口，要求用户必须登录才能访问

        /// <summary>
        /// The UpdateProfile
        /// </summary>
        /// <param name="dto">The dto<see cref="UpdateProfileDto"/></param>
        /// <returns>The <see cref="Task{IActionResult}"/></returns>
        [Authorize]
        [HttpPut("update-profile")] // 使用 PUT 表示更新资源
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            if (string.IsNullOrEmpty(dto.Username))
            {
                throw new ArgumentException("用户名不能为空");
            }
            if (string.IsNullOrEmpty(dto.Email))
            {
                throw new ArgumentException("邮箱不能为空");
            }
            if (!dto.Email.EndsWith(".com"))
            {
                throw new ArgumentException("请输入有效的邮箱地址");
            }
            if (string.IsNullOrEmpty(dto.Phone))
            {
                throw new ArgumentException("手机号不能为空");
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Phone, @"^\d{10,15}$"))
            {
                throw new ArgumentException("请输入有效的电话号码");
            }

            // 1. 解析当前登录者的 ID (依然是从 Token 拿，保证安全)
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new Exception("无法解析用户 ID");
            }

            // 2. 执行更新
            var success = await _authService.UpdateUserProfileAsync(userId, dto);

            if (!success.Item1)
            {
                throw new Exception(success.Item2); // 更新失败，抛出异常，交给全局异常处理中间件处理
            }

            return Ok(ApiResponse.OK());
        }

        /// <summary>
        /// The ForgotPasswordCode
        /// </summary>
        /// <param name="email">The email<see cref="string"/></param>
        /// <returns>The <see cref="Task{IActionResult}"/></returns>
        [HttpPost("forgot-password-code")]
        public async Task<IActionResult> ForgotPasswordCode([FromBody] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException("邮箱不能为空");
            }
            if (!email.EndsWith(".com"))
            {
                throw new ArgumentException("请输入有效的邮箱地址");
            }

            var result = await _authService.SendResetPasswordCodeAsync(email);
            if (result.success)
                return Ok(ApiResponse.OK());
            throw new Exception(result.message);
        }

        /// <summary>
        /// The ResetPassword
        /// </summary>
        /// <param name="request">The request<see cref="ResetPasswordRequestDto"/></param>
        /// <returns>The <see cref="Task{IActionResult}"/></returns>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            if (string.IsNullOrEmpty(request.Code))
            {
                throw new ArgumentException("验证码不能为空");
            }
            if (string.IsNullOrEmpty(request.Email))
            {
                throw new ArgumentException("邮箱不能为空");
            }
            if (!request.Email.EndsWith(".com"))
            {
                throw new ArgumentException("请输入有效的邮箱地址");
            }
            if (string.IsNullOrEmpty(request.NewPassword))
            {
                throw new ArgumentException("新密码不能为空");
            }

            var result = await _authService.ResetPasswordAsync(request.Email, request.Code, request.NewPassword);
            if (result.success)
                return Ok(ApiResponse.OK());
            throw new Exception(result.message);
        }
    }
}
