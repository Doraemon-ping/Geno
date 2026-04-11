using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Entities;
using 家谱.Models.Enums;

namespace 家谱.Services
{
    /// <summary>
    /// 系统公告服务接口。
    /// </summary>
    public interface IAnnouncementService
    {
        Task<PagedResult<AnnouncementViewDto>> QueryAsync(Guid? requesterId, AnnouncementQueryDto query);

        Task<AnnouncementViewDto> CreateAsync(Guid userId, AnnouncementCreateDto dto);

        Task<AnnouncementViewDto> UpdateAsync(Guid userId, Guid announcementId, AnnouncementUpdateDto dto);

        Task<bool> DeleteAsync(Guid userId, Guid announcementId);
    }

    /// <summary>
    /// 系统公告服务。
    /// </summary>
    public class AnnouncementService : IAnnouncementService
    {
        private readonly GenealogyDbContext _db;
        private readonly IAuditLogService _auditLogService;

        public AnnouncementService(GenealogyDbContext db, IAuditLogService auditLogService)
        {
            _db = db;
            _auditLogService = auditLogService;
        }

        public async Task<PagedResult<AnnouncementViewDto>> QueryAsync(Guid? requesterId, AnnouncementQueryDto query)
        {
            var canManage = requesterId.HasValue && await IsSystemAdminAsync(requesterId.Value);
            var publicOnly = query.PublicOnly || !canManage;
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 50);

            var dbQuery = _db.Announcements
                .AsNoTracking()
                .AsQueryable();

            if (publicOnly)
            {
                dbQuery = dbQuery.Where(item => item.Status == 1);
            }
            else if (query.Status.HasValue)
            {
                dbQuery = dbQuery.Where(item => item.Status == query.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                var category = query.Category.Trim();
                dbQuery = dbQuery.Where(item => item.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim();
                dbQuery = dbQuery.Where(item => item.Title.Contains(keyword) || item.Content.Contains(keyword));
            }

            var totalCount = await dbQuery.CountAsync();
            var items = await dbQuery
                .OrderByDescending(item => item.IsPinned)
                .ThenByDescending(item => item.PublishedAt ?? item.CreatedAt)
                .ThenByDescending(item => item.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<AnnouncementViewDto>
            {
                Items = await BuildViewAsync(items),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<AnnouncementViewDto> CreateAsync(Guid userId, AnnouncementCreateDto dto)
        {
            await EnsureSystemAdminAsync(userId);
            var entity = new SysAnnouncement
            {
                AnnouncementID = Guid.NewGuid(),
                Title = NormalizeRequired(dto.Title, "公告标题不能为空"),
                Content = NormalizeRequired(dto.Content, "公告内容不能为空"),
                Category = NormalizeCategory(dto.Category),
                Status = dto.PublishNow ? (byte)1 : (byte)0,
                IsPinned = dto.IsPinned,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PublishedAt = dto.PublishNow ? DateTime.UtcNow : null,
                IsDel = false
            };

            _db.Announcements.Add(entity);
            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync("Sys_Announcements", entity.AnnouncementID, "CREATE", userId, new { }, BuildSnapshot(entity));
            return (await BuildViewAsync(new[] { entity })).Single();
        }

        public async Task<AnnouncementViewDto> UpdateAsync(Guid userId, Guid announcementId, AnnouncementUpdateDto dto)
        {
            await EnsureSystemAdminAsync(userId);
            var entity = await _db.Announcements.FirstOrDefaultAsync(item => item.AnnouncementID == announcementId)
                ?? throw new KeyNotFoundException("公告不存在");
            var before = BuildSnapshot(entity);
            var wasPublished = entity.Status == 1;

            entity.Title = NormalizeRequired(dto.Title, "公告标题不能为空");
            entity.Content = NormalizeRequired(dto.Content, "公告内容不能为空");
            entity.Category = NormalizeCategory(dto.Category);
            entity.Status = dto.Status == 1 ? (byte)1 : (byte)0;
            entity.IsPinned = dto.IsPinned;
            entity.UpdatedAt = DateTime.UtcNow;
            if (!wasPublished && entity.Status == 1)
            {
                entity.PublishedAt = DateTime.UtcNow;
            }
            else if (entity.Status == 0)
            {
                entity.PublishedAt = null;
            }

            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync("Sys_Announcements", entity.AnnouncementID, "UPDATE", userId, before, BuildSnapshot(entity));
            return (await BuildViewAsync(new[] { entity })).Single();
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid announcementId)
        {
            await EnsureSystemAdminAsync(userId);
            var entity = await _db.Announcements.FirstOrDefaultAsync(item => item.AnnouncementID == announcementId);
            if (entity == null)
            {
                return false;
            }

            var before = BuildSnapshot(entity);
            entity.IsDel = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync("Sys_Announcements", entity.AnnouncementID, "DELETE", userId, before, BuildSnapshot(entity));
            return true;
        }

        private async Task EnsureSystemAdminAsync(Guid userId)
        {
            if (!await IsSystemAdminAsync(userId))
            {
                throw new UnauthorizedAccessException("只有系统管理员可以管理通知公告");
            }
        }

        private async Task<bool> IsSystemAdminAsync(Guid userId)
        {
            return await _db.Users
                .AsNoTracking()
                .AnyAsync(user => user.UserID == userId
                    && user.UserStatus == 1
                    && user.RoleType <= (byte)RoleType.Admin);
        }

        private async Task<List<AnnouncementViewDto>> BuildViewAsync(IReadOnlyCollection<SysAnnouncement> items)
        {
            var userIds = items.Select(item => item.CreatedBy).Distinct().ToList();
            var users = userIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await BuildGuidPredicateQuery(_db.Users.AsNoTracking(), user => user.UserID, userIds)
                    .ToDictionaryAsync(user => user.UserID, user => user.Username);

            return items.Select(item => new AnnouncementViewDto
            {
                AnnouncementId = item.AnnouncementID,
                Title = item.Title,
                Content = item.Content,
                Category = item.Category,
                Status = item.Status,
                StatusName = item.Status == 1 ? "已发布" : "草稿",
                IsPinned = item.IsPinned,
                CreatedBy = item.CreatedBy,
                CreatorName = users.TryGetValue(item.CreatedBy, out var username) ? username : "系统管理员",
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                PublishedAt = item.PublishedAt
            }).ToList();
        }

        private static string NormalizeRequired(string? value, string message)
        {
            var result = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new ArgumentException(message);
            }

            return result;
        }

        private static string NormalizeCategory(string? category)
        {
            return string.IsNullOrWhiteSpace(category) ? "系统公告" : category.Trim();
        }

        private static object BuildSnapshot(SysAnnouncement item) => new
        {
            item.AnnouncementID,
            item.Title,
            item.Content,
            item.Category,
            item.Status,
            item.IsPinned,
            item.CreatedBy,
            item.CreatedAt,
            item.UpdatedAt,
            item.PublishedAt,
            item.IsDel
        };

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

            return source.Where(Expression.Lambda<Func<TEntity, bool>>(body!, parameter));
        }
    }
}
