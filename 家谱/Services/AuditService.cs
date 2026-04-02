namespace 家谱.Services
{
    using Microsoft.EntityFrameworkCore;
    using 家谱.DB;
    using 家谱.Models.DTOs;
    using 家谱.Models.Entities;

    public interface IAuditService
    {
        // 用户：提交权限申请

        Task<(bool success, string message)> SubmitRoleUpgradeAsync(Guid userId, ApplyRoleRequestDto dto);

        // 管理员：获取待审核列表

        Task<List<AuditRequest>> GetPendingAuditsAsync();

        // 管理员：处理审核

        Task<(bool success, string message)> HandleAuditAsync(Guid processorId, HandleAuditDto dto);
    }

    public class AuditService : IAuditService
    {
        private readonly GenealogyDbContext _context;

        private readonly IMailService _mail;

        public AuditService(GenealogyDbContext context, IMailService mail)
        {
            _context = context;
            _mail = mail;
        }

        // 提交申请

        public async Task<(bool success, string message)> SubmitRoleUpgradeAsync(Guid userId, ApplyRoleRequestDto dto)
        {
            // 检查是否已有相同类型的待审核申请
            var hasPending = await _context.AuditRequests.AnyAsync(r =>
                r.ApplicantId == userId && r.Category == 2 && r.Status == 0);

            if (hasPending) return (false, "您已提交过权限申请，请勿重复操作");

            var audit = new AuditRequest
            {
                Id = new Guid(),
                ApplicantId = userId,
                Category = 2, // 权限晋升
                Reason = dto.Reason,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { NewRole = dto.TargetRole }),
                Status = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditRequests.Add(audit);

            var saved = await _context.SaveChangesAsync() > 0;
            // 2. 获取申请人信息（用于邮件展示）
            var user = await _context.Users.FindAsync(userId);
            if (saved)
            {
                await SendNotifyEmailToAdmins(user.Username, "权限晋升申请");

                return (true, "申请已提交，系统已通知管理员审核");
            }
            else
            {
                return (false, "提交失败，请稍后重试");

            }
        }

        // 处理审核（核心逻辑）

        public async Task<(bool success, string message)> HandleAuditAsync(Guid processorId, HandleAuditDto dto)
        {
            var request = await _context.AuditRequests.FindAsync(dto.RequestId);
            if (request == null || request.Status != 0) return (false, "申请不存在或已处理");

            var admin = await _context.Users.FindAsync(processorId);
            if (admin == null || admin.RoleType > 2) return (false, "无权限执行此操作");

            request.Status = dto.Action;
            request.AuditRemark = dto.AuditRemark;
            request.ProcessedAt = DateTime.UtcNow;

            // 如果审核通过，执行具体的业务变更
            if (dto.Action == 1)
            {
                if (request.Category == 2) // 权限晋升
                {
                    var payload = System.Text.Json.JsonDocument.Parse(request.PayloadJson);
                    Byte newRole = payload.RootElement.GetProperty("NewRole").GetByte();

                    var user = await _context.Users.FindAsync(request.ApplicantId);
                    if (user != null) user.RoleType = newRole;
                }
            }

            await _context.SaveChangesAsync();
            return (true, dto.Action == 1 ? "已核准通过" : "已驳回申请");
        }

        public async Task<List<AuditRequest>> GetPendingAuditsAsync()
        {
            return await _context.AuditRequests
                .Where(r => r.Status == 0)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        //FA

        private async Task SendNotifyEmailToAdmins(string applicantName, string taskType)
        {
            // 获取所有有权限的管理员邮箱
            var adminEmails = await _context.Users
                .Where(u => (u.RoleType == 1 || u.RoleType == 2) && !string.IsNullOrEmpty(u.Email))
                .Select(u => u.Email)
                .ToListAsync();

            if (adminEmails.Count == 0) return;

            string subject = "【赵氏家谱】新审核任务提醒";
            string body = $@"
        <div style='border:1px solid #8b4513; padding:20px; font-family:微软雅黑;'>
            <h2 style='color:#8b4513;'>新的审核申请待处理</h2>
            <p>尊敬的管理员：</p>
            <p>家族管理系统中收到了来自 <b>{applicantName}</b> 的 <b>{taskType}</b>。</p>
            <hr/>
            <p>请尽快登录管理后台查看详情并进行处理。</p>
            <p style='font-size:12px; color:#999;'>这是一封系统自动发送的邮件，请勿直接回复。</p>
        </div>";

            string emailList = "";
            foreach (var email in adminEmails)
            {
                // 调用你之前已经写好的 IMailService
                emailList += email + ";";

            }
            await _mail.SendEmailAsync(emailList, subject, body);
        }
    }
}
