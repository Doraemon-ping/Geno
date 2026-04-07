using Microsoft.AspNetCore.Authentication.JwtBearer; // 确保引用
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using 家谱.DB;
using 家谱.Middleware;
using 家谱.Services;
using 家谱.Setting;

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console() // 输出到控制台
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day) // 每天生成一个新文件
    .CreateLogger();

builder.Host.UseSerilog(); // 将 Serilog 集成到 ASP.NET Core

// 1. 从 appsettings.json 读取连接字符串
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// 2. 注册 DbContext 服务
builder.Services.AddDbContext<GenealogyDbContext>(options =>
    options.UseSqlServer(connectionString));
// Add services to the container.
builder.Services.AddControllers();
// 在 builder.Build() 之前添加
builder.Services.AddMemoryCache();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddScoped<IAuthService, AuthService>();
//builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IGenoPoemService, GenoPoemService>();
builder.Services.AddScoped<IGenoTreeService, GenoTreeService>();
builder.Services.AddScoped<IReviewService, ReviewService>();

// 绑定配置
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
// 注册服务为单例或瞬时
builder.Services.AddTransient<IMailService, MailService>();

// --- 步骤 1: 注册 Swagger 服务 ---
builder.Services.AddControllers(); // 确保控制器已注册
builder.Services.AddEndpointsApiExplorer(); // 使 API 可被发现
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "家谱 API", Version = "v1" });

    // 1. 定义安全定义 (Security Definition)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "请输入 JWT Token，格式为：Bearer {Your_Token}",
        Name = "Authorization",
        In = ParameterLocation.Header, // 确保在 Header 中
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // 2. 全局应用安全要求
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// 1. 定义策略
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader() // 必须允许自定义 Header
              .AllowAnyMethod();
    });
});

// --- 注册服务部分 ---

builder.Services.AddAuthentication(options =>
{
    // 关键修复：设置默认方案为 JwtBearer
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero, // 可选：禁用默认的 5 分钟过期宽限
        ValidateIssuerSigningKey = true,
        ValidIssuer = "GenealogyApi", // 必须和上面一模一样
        ValidAudience = "GenealogyApp",
        IssuerSigningKey = new
                        SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!))
    };
});

var app = builder.Build();

// 开启全局异常拦截
app.UseMiddleware<ExceptionMiddleware>();

// --- 步骤 2: 配置 Swagger 中间件 ---
// 建议仅在开发环境 (Development) 启用，生产环境建议关闭以保安全
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "家谱 API v1");
        // 如果想让 Swagger 成为首页（访问 http://localhost:xxxx/ 直接进入），设为空：
        // c.RoutePrefix = string.Empty;
    });
}

// 2. 使用中间件 (必须放在 UseAuthentication 之前)
app.UseCors("AllowAll");

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseRouting();
// 顺序必须是：认证 -> 授权
app.UseAuthentication(); // 1. 我是谁？(解析 Token)
app.UseAuthorization(); // 2. 我能做什么？(检查 [Authorize])
app.MapControllers();
app.Run();
