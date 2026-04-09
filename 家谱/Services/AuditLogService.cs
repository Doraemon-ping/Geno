using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 家谱.Common;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;
using 家谱.Models.Enums;

namespace 家谱.Services
{
    /// <summary>
    /// 数据库日志服务接口。
    /// </summary>
    public interface IAuditLogService
    {
        /// <summary>
        /// 写入一条数据变更日志。
        /// </summary>
        Task WriteAsync(
            string targetTable,
            Guid targetId,
            string opType,
            Guid? opUser,
            object? beforeData,
            object? afterData = null,
            Guid? taskId = null);

        /// <summary>
        /// 获取数据库日志列表。
        /// </summary>
        Task<List<DataLogDto>> GetLogsAsync(Guid requesterId, int take = 100);
    }

    /// <summary>
    /// 数据库日志服务。
    /// </summary>
    public class AuditLogService : IAuditLogService
    {
        private readonly GenealogyDbContext _db;

        public AuditLogService(GenealogyDbContext db)
        {
            _db = db;
        }

        public async Task WriteAsync(
            string targetTable,
            Guid targetId,
            string opType,
            Guid? opUser,
            object? beforeData,
            object? afterData = null,
            Guid? taskId = null)
        {
            var log = new DataLog
            {
                TaskID = taskId,
                TargetTable = targetTable,
                TargetID = targetId,
                BeforeData = JsonSerializer.Serialize(beforeData ?? new { }, JsonDefaults.Options),
                AfterData = JsonSerializer.Serialize(afterData ?? new { }, JsonDefaults.Options),
                OpType = opType,
                OpUser = opUser,
                CreatedAt = DateTime.UtcNow
            };

            _db.DataLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        public async Task<List<DataLogDto>> GetLogsAsync(Guid requesterId, int take = 100)
        {
            var requester = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.UserID == requesterId && user.UserStatus == 1)
                ?? throw new UnauthorizedAccessException("当前用户不存在或已被禁用");

            if (requester.RoleType is not ((byte)RoleType.SuperAdmin) and not ((byte)RoleType.Admin))
            {
                throw new UnauthorizedAccessException("只有超级管理员和管理员可以查看数据库日志");
            }

            var logs = await _db.DataLogs
                .AsNoTracking()
                .OrderByDescending(log => log.CreatedAt)
                .Take(Math.Clamp(take, 1, 200))
                .ToListAsync();

            var operatorMap = await _db.Users
                .AsNoTracking()
                .Where(user => user.UserStatus == 1)
                .ToDictionaryAsync(user => user.UserID, user => user.Username);

            var taskMap = await _db.ReviewTasks
                .AsNoTracking()
                .ToDictionaryAsync(task => task.TaskID, task => task.ActionCode);

            return logs.Select(log =>
            {
                string? actionCode = null;
                var hasAction = log.TaskID.HasValue && taskMap.TryGetValue(log.TaskID.Value, out actionCode);
                return new DataLogDto
                {
                    LogId = log.LogID,
                    TaskId = log.TaskID,
                    ActionCode = hasAction ? actionCode : null,
                    ActionName = hasAction ? ReviewActions.GetDisplayName(actionCode) : null,
                    TargetTable = log.TargetTable,
                    TargetId = log.TargetID,
                    OpType = log.OpType,
                    OperatorId = log.OpUser,
                    OperatorName = log.OpUser.HasValue && operatorMap.TryGetValue(log.OpUser.Value, out var username)
                        ? username
                        : "系统",
                    CreatedAt = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    BeforeData = DeserializeJson(log.BeforeData),
                    AfterData = DeserializeJson(log.AfterData)
                };
            }).ToList();
        }

        private static object? DeserializeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<object>(json, JsonDefaults.Options);
            }
            catch
            {
                return json;
            }
        }
    }
}
