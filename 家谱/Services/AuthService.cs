namespace 家谱.Services
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Tokens;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Text;
    using 家谱.DB;
    using 家谱.Models.DTOs;
    using 家谱.Models.Entities;
    using 家谱.Setting;
    using BCrypt = BCrypt;

    //= 认证服务接口，定义用户注册等相关方法 ==

    /// <summary>
    /// Defines the <see cref="IAuthService" />
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// The RegisterAsync
        /// </summary>
        /// <param name="dto">The dto<see cref="UserRegisterDto"/></param>
        /// <param name="inputCode">The inputCode<see cref="string"/></param>
        /// <returns>The <see cref="Task{(bool Success, string Message)}"/></returns>
        Task<(bool Success, string Message)> RegisterAsync(UserRegisterDto dto, string inputCode);

        /// <summary>
        /// The SendEmailCodeAsync
        /// </summary>
        /// <param name="email">The email<see cref="string"/></param>
        /// <returns>The <see cref="Task{(bool Success, string Message)}"/></returns>
        Task<(bool Success, string Message)> SendEmailCodeAsync(string email);

        /// <summary>
        /// The LoginAsync
        /// </summary>
        /// <param name="dto">The dto<see cref="LoginDto"/></param>
        /// <returns>The <see cref="Task{(bool Success, string? Token, string Message)}"/></returns>
        Task<(bool Success, string? Token, string Message)> LoginAsync(LoginDto dto);

        /// <summary>
        /// The GetUserProfileAsync
        /// </summary>
        /// <param name="userId">The userId<see cref="Guid"/></param>
        /// <returns>The <see cref="Task{UserDto?}"/></returns>
        Task<UserDto?> GetUserProfileAsync(Guid userId);

        /// <summary>
        /// The UpdateUserProfileAsync
        /// </summary>
        /// <param name="userId">The userId<see cref="Guid"/></param>
        /// <param name="dto">The dto<see cref="UpdateProfileDto"/></param>
        /// <returns>The <see cref="Task{(bool, string)}"/></returns>
        Task<(bool, string)> UpdateUserProfileAsync(Guid userId, UpdateProfileDto dto);

        // ：密码重置方法

        /// <summary>
        /// The ResetPasswordAsync
        /// </summary>
        /// <param name="email">The email<see cref="string"/></param>
        /// <param name="code">The code<see cref="string"/></param>
        /// <param name="newPassword">The newPassword<see cref="string"/></param>
        /// <returns>The <see cref="Task{(bool success, string message)}"/></returns>
        Task<(bool success, string message)> ResetPasswordAsync(string email, string code, string newPassword);

        /// <summary>
        /// The SendResetPasswordCodeAsync
        /// </summary>
        /// <param name="email">The email<see cref="string"/></param>
        /// <returns>The <see cref="Task{(bool success, string message)}"/></returns>
        Task<(bool success, string message)> SendResetPasswordCodeAsync(string email);
    }

    /// <summary>
    /// Defines the <see cref="AuthService" />
    /// </summary>
    public class AuthService : IAuthService
    {
        /// <summary>
        /// Defines the _context
        /// </summary>
        private readonly GenealogyDbContext _context;

        /// <summary>
        /// Defines the _mailService
        /// </summary>
        private readonly IMailService _mailService;

        /// <summary>
        /// Defines the _cache
        /// </summary>
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Defines the _jwtSettings
        /// </summary>
        private readonly JwtSettings _jwtSettings;// 1. 定义私有字段

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthService"/> class.
        /// </summary>
        /// <param name="context">The context<see cref="GenealogyDbContext"/></param>
        /// <param name="mailService">The mailService<see cref="IMailService"/></param>
        /// <param name="cache">The cache<see cref="IMemoryCache"/></param>
        /// <param name="settings">The settings<see cref="IOptions{JwtSettings}"/></param>
        public AuthService(GenealogyDbContext context,
                            IMailService mailService,
                            IMemoryCache cache,
                            IOptions<JwtSettings> settings)
        {
            _context = context;
            _mailService = mailService;
            _cache = cache;
            _jwtSettings = settings.Value; // 2. 通过构造函数注入配置对象
        }

        //注册

        /// <summary>
        /// The SendEmailCodeAsync
        /// </summary>
        /// <param name="email">The email<see cref="string"/></param>
        /// <returns>The <see cref="Task{(bool Success, string Message)}"/></returns>
        public async Task<(bool Success, string Message)> SendEmailCodeAsync(string email)
        {
            // 1. 生成 6 位随机数字
            var code = new Random().Next(100000,
            999999).ToString();

            // 2. 将验证码存入缓存，设置 5 分钟过期
            // Key 格式为 "VCode_邮箱地址"
            var cacheKey = $"VCode_{email}";
            _cache.Set(cacheKey, code, TimeSpan.FromMinutes(5));

            // 3. 发送邮件
            var subject = "【家谱系统】您的身份验证码";
            var body = $"""
            <div style="border:1px solid #eee; padding:20px;">
                <h2 style="color:#8b4513;">身份验证</h2>
                <p>您正在注册家谱系统，您的验证码为：</p>
                <p style="font-size:24px; font-weight:bold; color:#d44d44;">{code}</p>
                <p>该验证码 5 分钟内有效。如果不是您本人操作，请忽略此邮件。</p>
            </div>
            """;

            return await _mailService.SendEmailAsync(email, subject, body)
                ? (true,
            "验证码已发送")
                : (false,
            "邮件发送失败");
        }

        /// <summary>
        /// The RegisterAsync
        /// </summary>
        /// <param name="dto">The dto<see cref="UserRegisterDto"/></param>
        /// <param name="inputCode">The inputCode<see cref="string"/></param>
        /// <returns>The <see cref="Task{(bool Success, string Message)}"/></returns>
        public async Task<(bool Success, string Message)> RegisterAsync(UserRegisterDto dto, string inputCode)
        {
            // 1. 从缓存获取验证码
            var cacheKey = $"VCode_{dto.Email}";
            if (!_cache.TryGetValue(cacheKey, out string? savedCode))
                return (false,
            "验证码已过期或未获取");

            // 2. 校验验证码
            if (savedCode != inputCode)
                return (false,
            "验证码错误");

            // 3. 校验通过后，清理缓存
            _cache.Remove(cacheKey);

            // 4. 执行原有的数据库写入逻辑
            // 1. 检查用户名是否已存在
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username && u.UserStatus == 1))
                return (false,
            "用户名已被占用");

            // 2. 检查邮箱是否已存在
            if (!string.IsNullOrEmpty(dto.Email) && await _context.Users.AnyAsync(u => u.Email == dto.Email && u.UserStatus == 1))
                return (false,
            "该邮箱已被注册");

            // 3. 密码哈希加密
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // 4. 构建实体类
            var newUser = new SysUser
            {
                UserID = Guid.NewGuid(),
                Username = dto.Username,
                Email = dto.Email,
                Phone = dto.Phone,
                PasswordHash = passwordHash,
                RoleType = 3, // 默认访客
                UserStatus = 1, // 激活
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            var IsSave = await _context.SaveChangesAsync();

            // 5. 发送欢迎邮件 (异步非阻塞)
            if (!string.IsNullOrEmpty(dto.Email) && IsSave > 0)
            {
                var subject = "【家谱系统】账号注册成功通知";
                var body = $"<h3>欢迎，{dto.Username}！</h3><p>您的家谱系统账号已开通，请妥善保管密码。</p>";
                _ = _mailService.SendEmailAsync(dto.Email, subject, body);
                return (true,
                "注册成功");
            }
            else
            {
                return (false,
                "注册失败");
            }
        }

        // 登录方法，返回 JWT 令牌

        /// <summary>
        /// The LoginAsync
        /// </summary>
        /// <param name="dto">The dto<see cref="LoginDto"/></param>
        /// <returns>The <see cref="Task{(bool Success, string? Token, string Message)}"/></returns>
        public async Task<(bool Success, string? Token, string Message)> LoginAsync(LoginDto dto)
        {
            // 1. 查找用户
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null) return (false,
            null,
            "用户名或密码错误");

            // 2. 检查状态
            if (user.UserStatus == 0) return (false,
            null,
            "账号已被禁用，请联系管理员");

            // 3. 验证 BCrypt 哈希密码
            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return (false,
            null,
            "用户名或密码错误");

            // 4. 准备 Claims (身份载荷)
            var claims = new[]
            {
        new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Role, user.RoleType.ToString()),
        new Claim("Email", user.Email ?? "")
            };

            // 5. 生成密钥和令牌
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes), // 过期时间也用 UTC
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (true, tokenString,
            "登录成功");
        }

        // 获取用户资料的方法

        /// <summary>
        /// The GetUserProfileAsync
        /// </summary>
        /// <param name="userId">The userId<see cref="Guid"/></param>
        /// <returns>The <see cref="Task{UserDto?}"/></returns>
        public async Task<UserDto?> GetUserProfileAsync(Guid userId)
        {
            var user = await _context.Users
                .Where(u => u.UserID == userId && u.UserStatus == 1)
                .Select(u => new UserDto
                {
                    Username = u.Username,
                    Email = u.Email,
                    Phone = u.Phone,
                    RoleType = u.RoleType,
                })
                .FirstOrDefaultAsync();

            return user;
        }

        // 更新用户资料的方法

        /// <summary>
        /// The UpdateUserProfileAsync
        /// </summary>
        /// <param name="userId">The userId<see cref="Guid"/></param>
        /// <param name="dto">The dto<see cref="UpdateProfileDto"/></param>
        /// <returns>The <see cref="Task{(bool, string)}"/></returns>
        public async Task<(bool, string)> UpdateUserProfileAsync(Guid userId, UpdateProfileDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user.UserStatus == 0)
                return (false, "账号已被禁用！");

            if (user == null) return (false,
            "请先注册");

            if (user.Username == dto.Username)
            {
                return (false,
                "用户名已注册");
            }

            if (user.Email == dto.Email)
            {
                return (false,
                "该邮箱已被注册");
            }

            if (user.Phone == dto.Phone)
            {
                return (false,
                "该手机号已被注册");
            }
            // 仅更新传了值的字段
            if (!string.IsNullOrEmpty(dto.Username)) user.Username = dto.Username;
            if (!string.IsNullOrEmpty(dto.Phone)) user.Phone = dto.Phone;
            if (!string.IsNullOrEmpty(dto.Email)) user.Email = dto.Email;

            return (await _context.SaveChangesAsync() > 0,
            "修改成功");
        }

        // 4. 发送重置密码验证码

        /// <summary>
        /// The SendResetPasswordCodeAsync
        /// </summary>
        /// <param name="email">The email<see cref="string"/></param>
        /// <returns>The <see cref="Task{(bool success, string message)}"/></returns>
        public async Task<(bool success, string message)> SendResetPasswordCodeAsync(string email)
        {
            // 1. 检查用户是否存在
            var userExists = await _context.Users.AnyAsync(u => u.Email == email && u.UserStatus == 1);
            if (!userExists) return (false,
            "该邮箱尚未注册");

            // 2. 生成 6 位验证码
            var code = new Random().Next(100000,
            999999).ToString();

            // 3. 存入缓存（有效期 5 分钟）
            _cache.Set($"ResetCode_{email}", code, TimeSpan.FromMinutes(5));

            // 4. 发送邮件
            await _mailService.SendEmailAsync(email,
            "【赵氏家谱】重置密码验证码",
                $"您正在申请找回密码，验证码为：<b>{code}</b>。5分钟内有效，如非本人操作请忽略。");

            return (true,
            "验证码已发送至您的邮箱");
        }

        // 5. 密码重置方法

        /// <summary>
        /// The ResetPasswordAsync
        /// </summary>
        /// <param name="email">The email<see cref="string"/></param>
        /// <param name="code">The code<see cref="string"/></param>
        /// <param name="newPassword">The newPassword<see cref="string"/></param>
        /// <returns>The <see cref="Task{(bool success, string message)}"/></returns>
        public async Task<(bool success, string message)> ResetPasswordAsync(string email, string code, string newPassword)
        {
            // 1. 验证码校验
            if (!_cache.TryGetValue($"ResetCode_{email}", out string? savedCode) || savedCode != code)
            {
                return (false,
                "验证码错误或已过期");
            }
            // 2. 找到用户
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return (false,
            "用户不存在");

            // 3. 加密并更新密码 (假设你使用了 BCrypt 或类似的加密)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            var result = await _context.SaveChangesAsync();
            if (result > 0)
            {
                _cache.Remove($"ResetCode_{email}"); // 成功后清除验证码
                return (true,
                "密码重置成功，请重新登录");
            }

            return (false,
            "数据库更新失败");
        }
    }
}
