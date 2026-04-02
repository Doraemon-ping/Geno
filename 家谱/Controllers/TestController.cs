using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using 家谱.Services;
using 家谱.Setting;

namespace 家谱.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IMailService _mailService;
        private readonly IOptions<MailSettings> _mailSettings;

        public TestController(IMailService mailService, IOptions<MailSettings> mailSettings)
        {
            _mailService = mailService;
            _mailSettings = mailSettings;
        }

        /// <summary>
        /// 诊断接口：检查配置是否正确加载
        /// </summary>
        [HttpGet("diagnose-config")]
        public IActionResult DiagnoseConfig()
        {
            var settings = _mailSettings.Value;

            // 注意：生产环境切勿返回 Password，这里仅用于开发调试显示前两位
            var maskedPassword = string.IsNullOrEmpty(settings.Password)
                ? "MISSING"
                : settings.Password.Substring(0, Math.Min(2, settings.Password.Length)) + "****";

            return Ok(new
            {
                Server = settings.Server,
                Port = settings.Port,
                Sender = settings.SenderEmail,
                HasPassword = !string.IsNullOrEmpty(settings.Password),
                PasswordPreview = maskedPassword,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 集成测试：发送一封真实的测试邮件
        /// </summary>
        [HttpPost("send-test-mail")]
        public async Task<IActionResult> SendTestMail([FromQuery] string targetEmail)
        {
            if (string.IsNullOrEmpty(targetEmail))
                return BadRequest("请提供接收测试邮件的邮箱地址。");

            var subject = "🚀 家谱系统集成测试邮件";
            var body = $"""
            <div style="font-family: sans-serif; border: 1px solid #eee; padding: 20px;">
                <h2 style="color: #2c3e50;">家谱系统邮件测试成功！</h2>
                <p>这是一封由 API 自动生成的集成测试邮件。</p>
                <ul>
                    <li><strong>发送时间：</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</li>
                    <li><strong>环境名称：</strong> {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}</li>
                </ul>
                <p style="color: #7f8c8d; font-size: 0.9em;">如果收到此邮件，说明你的 SMTP 配置与 MailService 链路已完全打通。</p>
            </div>
            """;

            var success = await _mailService.SendEmailAsync(targetEmail, subject, body);

            if (success)
            {
                return Ok(new { Message = $"测试邮件已发送至 {targetEmail}，请检查收件箱（及垃圾箱）。" });
            }

            return StatusCode(500, new
            {
                Message = "邮件发送失败。可能是 SMTP 配置错误、授权码失效或服务器防火墙拦截了端口。",
                CheckList = new[] { "检查 465/587 端口是否开启", "确认使用授权码而非登录密码", "确认 SMTP 服务器地址无误" }
            });
        }
    }
}
