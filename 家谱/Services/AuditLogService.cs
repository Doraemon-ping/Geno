using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 家谱.Common;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
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
        /// 兼容旧版的日志列表读取。
        /// </summary>
        Task<List<DataLogDto>> GetLogsAsync(Guid requesterId, int take = 100);

        /// <summary>
        /// 分页查询数据库日志。
        /// </summary>
        Task<PagedResult<DataLogDto>> QueryLogsAsync(Guid requesterId, DataLogQueryDto query);
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
            var paged = await QueryLogsAsync(requesterId, new DataLogQueryDto
            {
                Page = 1,
                PageSize = Math.Clamp(take, 1, 200)
            });

            return paged.Items.ToList();
        }

        public async Task<PagedResult<DataLogDto>> QueryLogsAsync(Guid requesterId, DataLogQueryDto query)
        {
            await EnsureCanViewLogsAsync(requesterId);

            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var logQuery = _db.DataLogs
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.TargetTable))
            {
                var targetTable = query.TargetTable.Trim();
                logQuery = logQuery.Where(log => log.TargetTable == targetTable);
            }

            if (!string.IsNullOrWhiteSpace(query.OpType))
            {
                var opType = query.OpType.Trim().ToUpper();
                logQuery = logQuery.Where(log => log.OpType == opType);
            }

            if (query.CreatedFrom.HasValue)
            {
                logQuery = logQuery.Where(log => log.CreatedAt >= query.CreatedFrom.Value);
            }

            if (query.CreatedTo.HasValue)
            {
                logQuery = logQuery.Where(log => log.CreatedAt <= query.CreatedTo.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim();
                if (Guid.TryParse(keyword, out var parsedGuid))
                {
                    logQuery = logQuery.Where(log =>
                        log.TaskID == parsedGuid ||
                        log.TargetTable.Contains(keyword) ||
                        log.OpType.Contains(keyword));
                }
                else
                {
                    logQuery = logQuery.Where(log =>
                        log.TargetTable.Contains(keyword) ||
                        log.OpType.Contains(keyword));
                }
            }

            var totalCount = await logQuery.CountAsync();
            var logs = await logQuery
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.LogID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var operatorIds = logs
                .Where(log => log.OpUser.HasValue)
                .Select(log => log.OpUser!.Value)
                .Distinct()
                .ToList();

            var taskIds = logs
                .Where(log => log.TaskID.HasValue)
                .Select(log => log.TaskID!.Value)
                .Distinct()
                .ToList();

            var operatorMap = operatorIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await BuildGuidPredicateQuery(_db.Users.AsNoTracking(), user => user.UserID, operatorIds)
                    .ToDictionaryAsync(user => user.UserID, user => user.Username);

            var taskMap = taskIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await BuildGuidPredicateQuery(_db.ReviewTasks.AsNoTracking(), task => task.TaskID, taskIds)
                    .ToDictionaryAsync(task => task.TaskID, task => task.ActionCode);

            var items = logs.Select(log =>
            {
                taskMap.TryGetValue(log.TaskID ?? Guid.Empty, out var actionCode);
                return new DataLogDto
                {
                    LogId = log.LogID,
                    TaskId = log.TaskID,
                    ActionCode = actionCode,
                    ActionName = actionCode == null ? null : ReviewActions.GetDisplayName(actionCode),
                    TargetTable = log.TargetTable,
                    OpType = log.OpType,
                    OperatorName = log.OpUser.HasValue && operatorMap.TryGetValue(log.OpUser.Value, out var username)
                        ? username
                        : "系统",
                    CreatedAt = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    BeforeData = DeserializeJson(log.BeforeData),
                    AfterData = DeserializeJson(log.AfterData)
                };
            }).ToList();

            return new PagedResult<DataLogDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        private async Task EnsureCanViewLogsAsync(Guid requesterId)
        {
            var requester = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.UserID == requesterId && user.UserStatus == 1)
                ?? throw new UnauthorizedAccessException("当前用户不存在或已被禁用");

            if (requester.RoleType is not ((byte)RoleType.SuperAdmin) and not ((byte)RoleType.Admin))
            {
                throw new UnauthorizedAccessException("只有超级管理员和管理员可以查看数据库日志");
            }
        }

        private static IQueryable<TEntity> BuildGuidPredicateQuery<TEntity>(
            IQueryable<TEntity> source,
            Expression<Func<TEntity, Guid>> selector,
            IReadOnlyCollection<Guid> ids)
        {
            if (ids.Count == 0)
            {
                return source.Where(_ => false);
            }

            var parameter = selector.Parameters[0];
            Expression? body = null;
            foreach (var id in ids)
            {
                var equals = Expression.Equal(selector.Body, Expression.Constant(id));
                body = body == null ? equals : Expression.OrElse(body, equals);
            }

            var predicate = Expression.Lambda<Func<TEntity, bool>>(body!, parameter);
            return source.Where(predicate);
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
