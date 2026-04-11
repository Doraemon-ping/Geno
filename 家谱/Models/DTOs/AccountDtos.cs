namespace 家谱.Models.DTOs
{
    //Account接口相关的DTO类

    //= 用户注册 DTO ==

    /// <summary>
    /// Defines the <see cref="UserRegisterDto" />
    /// </summary>
    public class UserRegisterDto
    {
        /// <summary>
        /// Gets or sets the Username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Password
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Email
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the Phone
        /// </summary>
        public string? Phone { get; set; }
    }

    //= 密码重置请求 DTO ==

    /// <summary>
    /// Defines the <see cref="ResetPasswordRequestDto" />
    /// </summary>
    public class ResetPasswordRequestDto
    {
        /// <summary>
        /// Gets or sets the Email
        /// </summary>
        public string Email { get; set; } = "";

        /// <summary>
        /// Gets or sets the Code
        /// </summary>
        public string Code { get; set; } = "";

        /// <summary>
        /// Gets or sets the NewPassword
        /// </summary>
        public string NewPassword { get; set; } = "";
    }

    /// <summary>
    /// Defines the <see cref="LoginDto" />
    /// 用户登录，包含登录所需的用户名和密码
    /// </summary>
    public class LoginDto
    {
        /// <summary>
        /// Gets or sets the Username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Password
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Defines the <see cref="UserDto" />
    /// 用户信息 DTO，包含用户的基本信息和角色信息，供前端展示使用
    /// </summary>
    public class UserDto
    {
        /// <summary>
        /// Gets or sets the UserId
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the Username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Email
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the Phone
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// 用户头像地址。
        /// </summary>
        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// Gets or sets the RoleType
        /// </summary>
        public byte RoleType { get; set; }

        /// <summary>
        /// Gets the RoleName
        /// </summary>
        public string RoleName => RoleType switch
        {
            0 => "超级管理员",
            1 => "管理员",
            2 => "修谱员",
            _ => "访客"
        };

        /// <summary>
        /// Gets or sets the CreatedAt
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Defines the <see cref="UpdateProfileDto" />
    /// </summary>
    public class UpdateProfileDto
    {
        /// <summary>
        /// Gets or sets the Username
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the Phone
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Gets or sets the Email
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// 用户头像地址。
        /// </summary>
        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? AvatarUrl { get; set; }
    }

    /// <summary>
    /// 系统用户搜索结果。
    /// </summary>
    public class UserLookupDto
    {
        /// <summary>
        /// 用户标识。
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 用户名。
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱。
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// 电话。
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// 用户头像地址。
        /// </summary>
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// 角色类型。
        /// </summary>
        public byte RoleType { get; set; }

        /// <summary>
        /// 角色名称。
        /// </summary>
        public string RoleName => RoleType switch
        {
            0 => "超级管理员",
            1 => "管理员",
            2 => "修谱员",
            _ => "访客"
        };
    }

}
