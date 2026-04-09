using System.Text.Json;
using 家谱.Common;
using 家谱.DB;
using 家谱.Models.Entities;

namespace 家谱.Services
{
    public interface IAuditLogService
    {
        Task WriteAsync(string targetTable, Guid targetId, string opType, Guid? opUser, object? beforeData, Guid? taskId = null);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly GenealogyDbContext _db;

        public AuditLogService(GenealogyDbContext db)
        {
            _db = db;
        }

        public async Task WriteAsync(string targetTable, Guid targetId, string opType, Guid? opUser, object? beforeData, Guid? taskId = null)
        {
            var log = new DataLog
            {
                TaskID = taskId,
                TargetTable = targetTable,
                TargetID = targetId,
                BeforeData = JsonSerializer.Serialize(beforeData ?? new { }, JsonDefaults.Options),
                OpType = opType,
                OpUser = opUser,
                CreatedAt = DateTime.UtcNow
            };

            _db.DataLogs.Add(log);
            await _db.SaveChangesAsync();
        }
    }
}
