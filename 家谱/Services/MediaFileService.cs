using System.Linq.Expressions;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;
using 家谱.Models.Enums;
using 家谱.Setting;

namespace 家谱.Services
{
    public interface IMediaFileService
    {
        Task<MediaFileDto> SaveTempAsync(IFormFile file, Guid uploaderId, Guid? treeId, string? caption = null, int sortOrder = 1);

        Task<MediaFileDto> SaveTempAsync(IFormFile file, Guid uploaderId, Guid? treeId, string ownerType, string? caption = null, int sortOrder = 1);

        Task EnsureMediaEditableAsync(IEnumerable<Guid> mediaIds, Guid requesterId, Guid? treeId, Guid? currentOwnerId = null, string ownerType = "event");

        Task MarkPendingAsync(IEnumerable<Guid> mediaIds, Guid submitterId, Guid reviewTaskId);

        Task MarkRejectedAsync(Guid reviewTaskId, Guid operatorUserId);

        Task SyncEventMediaAsync(Guid eventId, Guid? treeId, IEnumerable<Guid> mediaIds, Guid operatorUserId, Guid? reviewTaskId = null);

        Task SyncMemberMediaAsync(Guid memberId, Guid treeId, IEnumerable<Guid> mediaIds, Guid operatorUserId, Guid? reviewTaskId = null);

        Task SyncPostMediaAsync(Guid postId, Guid treeId, IEnumerable<Guid> mediaIds, Guid operatorUserId);

        Task SoftDeleteByOwnerAsync(string ownerType, Guid ownerId, Guid operatorUserId, Guid? taskId = null);

        Task<List<MediaFileDto>> GetByOwnerAsync(string ownerType, Guid ownerId, bool includeAllStatuses = false);

        Task<List<MediaFileDto>> GetByIdsAsync(IEnumerable<Guid> mediaIds);
    }

    public class MediaFileService : IMediaFileService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
            ".mp4", ".mov", ".avi", ".webm", ".m4v",
            ".pdf", ".txt", ".doc", ".docx"
        };

        private readonly GenealogyDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private readonly IAuditLogService _auditLogService;
        private readonly MediaStorageSettings _settings;

        public MediaFileService(
            GenealogyDbContext db,
            IWebHostEnvironment environment,
            IAuditLogService auditLogService,
            IOptions<MediaStorageSettings> settings)
        {
            _db = db;
            _environment = environment;
            _auditLogService = auditLogService;
            _settings = settings.Value;
        }

        public Task<MediaFileDto> SaveTempAsync(IFormFile file, Guid uploaderId, Guid? treeId, string? caption = null, int sortOrder = 1)
            => SaveTempAsync(file, uploaderId, treeId, "event", caption, sortOrder);

        public async Task<MediaFileDto> SaveTempAsync(IFormFile file, Guid uploaderId, Guid? treeId, string ownerType, string? caption = null, int sortOrder = 1)
        {
            if (file == null || file.Length <= 0)
            {
                throw new InvalidOperationException("涓婁紶鏂囦欢涓嶈兘涓虹┖");
            }

            var maxFileSize = Math.Max(_settings.MaxFileSizeMb, 1) * 1024L * 1024L;
            if (file.Length > maxFileSize)
            {
                throw new InvalidOperationException($"鍗曚釜鏂囦欢澶у皬涓嶈兘瓒呰繃 {_settings.MaxFileSizeMb}MB");
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("暂不支持该文件类型");
            }

            ownerType = NormalizeOwnerType(ownerType);
            var mediaId = Guid.NewGuid();
            var monthFolder = DateTime.UtcNow.ToString("yyyyMM");
            var relativeFolder = Path.Combine(ownerType, monthFolder);
            var physicalFolder = Path.Combine(GetMediaRootPath(), relativeFolder);
            Directory.CreateDirectory(physicalFolder);

            await using var source = file.OpenReadStream();
            using var memory = new MemoryStream();
            await source.CopyToAsync(memory);
            var bytes = memory.ToArray();

            var fileName = $"{mediaId}{extension.ToLowerInvariant()}";
            var physicalPath = Path.Combine(physicalFolder, fileName);
            await File.WriteAllBytesAsync(physicalPath, bytes);

            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var publicUrl = $"{NormalizeRequestPath(_settings.RequestPath)}/{relativeFolder.Replace("\\", "/")}/{fileName}";

            var entity = new SysMediaFile
            {
                MediaID = mediaId,
                TreeID = NormalizeTreeId(treeId),
                OwnerType = ownerType,
                OwnerID = null,
                FileName = Path.GetFileName(file.FileName),
                FileExt = extension.ToLowerInvariant(),
                MimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                FileSize = file.Length,
                StoragePath = physicalPath,
                PublicUrl = publicUrl,
                HashValue = hash,
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
                SortOrder = sortOrder <= 0 ? 1 : sortOrder,
                UploadUserID = uploaderId,
                Status = (byte)MediaFileStatus.Temp,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDel = false
            };

            _db.MediaFiles.Add(entity);
            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Sys_Media_Files",
                entity.MediaID,
                "CREATE",
                uploaderId,
                new { },
                BuildLogSnapshot(entity),
                null);

            return MapDto(entity);
        }

        public async Task EnsureMediaEditableAsync(IEnumerable<Guid> mediaIds, Guid requesterId, Guid? treeId, Guid? currentOwnerId = null, string ownerType = "event")
        {
            var ids = DistinctIds(mediaIds);
            if (ids.Count == 0)
            {
                return;
            }

            ownerType = NormalizeOwnerType(ownerType);
            var entities = await GetEntitiesByIdsInternalAsync(ids, true);
            if (entities.Count != ids.Count)
            {
                throw new KeyNotFoundException("瀛樺湪鏈壘鍒扮殑濯掍綋鏂囦欢");
            }

            var normalizedTreeId = NormalizeTreeId(treeId);
            foreach (var entity in entities)
            {
                if (entity.IsDel)
                {
                    throw new InvalidOperationException("瀛樺湪宸插垹闄ょ殑濯掍綋鏂囦欢");
                }

                if (entity.TreeID != normalizedTreeId)
                {
                    throw new InvalidOperationException("媒体文件不属于当前业务范围");
                }

                if (entity.OwnerID == null)
                {
                    if (entity.UploadUserID != requesterId)
                    {
                        throw new UnauthorizedAccessException("只能使用自己上传的临时媒体文件");
                    }

                    continue;
                }

                if (currentOwnerId.HasValue && entity.OwnerType == ownerType && entity.OwnerID == currentOwnerId.Value)
                {
                    continue;
                }

                throw new InvalidOperationException("存在已绑定到其他业务对象的媒体文件");
            }
        }

        public async Task MarkPendingAsync(IEnumerable<Guid> mediaIds, Guid submitterId, Guid reviewTaskId)
        {
            var ids = DistinctIds(mediaIds);
            if (ids.Count == 0)
            {
                return;
            }

            var entities = await GetEntitiesByIdsInternalAsync(ids, true);
            foreach (var entity in entities)
            {
                if (entity.OwnerID.HasValue && entity.Status == (byte)MediaFileStatus.Approved)
                {
                    continue;
                }

                var before = BuildLogSnapshot(entity);
                entity.Status = (byte)MediaFileStatus.Pending;
                entity.ReviewTaskID = reviewTaskId;
                entity.UpdatedAt = DateTime.UtcNow;

                await _auditLogService.WriteAsync(
                    "Sys_Media_Files",
                    entity.MediaID,
                    "UPDATE",
                    submitterId,
                    before,
                    BuildLogSnapshot(entity),
                    reviewTaskId);
            }

            await _db.SaveChangesAsync();
        }

        public async Task MarkRejectedAsync(Guid reviewTaskId, Guid operatorUserId)
        {
            var files = await _db.MediaFiles
                .Where(item => item.ReviewTaskID == reviewTaskId && !item.IsDel)
                .ToListAsync();

            foreach (var file in files)
            {
                if (file.Status == (byte)MediaFileStatus.Approved)
                {
                    continue;
                }

                var before = BuildLogSnapshot(file);
                file.Status = (byte)MediaFileStatus.Rejected;
                file.UpdatedAt = DateTime.UtcNow;

                await _auditLogService.WriteAsync(
                    "Sys_Media_Files",
                    file.MediaID,
                    "UPDATE",
                    operatorUserId,
                    before,
                    BuildLogSnapshot(file),
                    reviewTaskId);
            }

            await _db.SaveChangesAsync();
        }

        public Task SyncEventMediaAsync(Guid eventId, Guid? treeId, IEnumerable<Guid> mediaIds, Guid operatorUserId, Guid? reviewTaskId = null)
            => SyncOwnerMediaAsync("event", eventId, NormalizeTreeId(treeId), mediaIds, operatorUserId, reviewTaskId);

        public Task SyncMemberMediaAsync(Guid memberId, Guid treeId, IEnumerable<Guid> mediaIds, Guid operatorUserId, Guid? reviewTaskId = null)
            => SyncOwnerMediaAsync("member", memberId, treeId, mediaIds, operatorUserId, reviewTaskId);

        public Task SyncPostMediaAsync(Guid postId, Guid treeId, IEnumerable<Guid> mediaIds, Guid operatorUserId)
            => SyncOwnerMediaAsync("post", postId, treeId, mediaIds, operatorUserId, null);

        public async Task SoftDeleteByOwnerAsync(string ownerType, Guid ownerId, Guid operatorUserId, Guid? taskId = null)
        {
            var files = await _db.MediaFiles
                .Where(item => item.OwnerType == NormalizeOwnerType(ownerType) && item.OwnerID == ownerId && !item.IsDel)
                .ToListAsync();

            foreach (var file in files)
            {
                var before = BuildLogSnapshot(file);
                file.IsDel = true;
                file.Status = (byte)MediaFileStatus.Archived;
                file.UpdatedAt = DateTime.UtcNow;

                await _auditLogService.WriteAsync(
                    "Sys_Media_Files",
                    file.MediaID,
                    "DELETE",
                    operatorUserId,
                    before,
                    BuildLogSnapshot(file),
                    taskId);
            }

            await _db.SaveChangesAsync();
        }

        public async Task<List<MediaFileDto>> GetByOwnerAsync(string ownerType, Guid ownerId, bool includeAllStatuses = false)
        {
            var query = _db.MediaFiles
                .AsNoTracking()
                .Where(item => item.OwnerType == NormalizeOwnerType(ownerType) && item.OwnerID == ownerId);

            if (!includeAllStatuses)
            {
                query = query.Where(item => item.Status == (byte)MediaFileStatus.Approved);
            }

            return (await query
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.CreatedAt)
                    .ToListAsync())
                .Select(MapDto)
                .ToList();
        }

        public async Task<List<MediaFileDto>> GetByIdsAsync(IEnumerable<Guid> mediaIds)
        {
            var ids = DistinctIds(mediaIds);
            if (ids.Count == 0)
            {
                return new List<MediaFileDto>();
            }

            return (await GetEntitiesByIdsInternalAsync(ids, false))
                .Select(MapDto)
                .ToList();
        }

        private async Task SyncOwnerMediaAsync(string ownerType, Guid ownerId, Guid? treeId, IEnumerable<Guid> mediaIds, Guid operatorUserId, Guid? reviewTaskId)
        {
            ownerType = NormalizeOwnerType(ownerType);
            var ids = DistinctIds(mediaIds);
            var existingFiles = await _db.MediaFiles
                .IgnoreQueryFilters()
                .Where(item => item.OwnerType == ownerType && item.OwnerID == ownerId)
                .ToListAsync();

            var existingMap = existingFiles.ToDictionary(item => item.MediaID);
            var normalizedTreeId = NormalizeTreeId(treeId);

            foreach (var existing in existingFiles)
            {
                if (ids.Contains(existing.MediaID))
                {
                    var before = BuildLogSnapshot(existing);
                    existing.TreeID = normalizedTreeId;
                    existing.OwnerType = ownerType;
                    existing.OwnerID = ownerId;
                    existing.Status = (byte)MediaFileStatus.Approved;
                    existing.ReviewTaskID = reviewTaskId;
                    existing.IsDel = false;
                    existing.UpdatedAt = DateTime.UtcNow;

                    await _auditLogService.WriteAsync(
                        "Sys_Media_Files",
                        existing.MediaID,
                        "UPDATE",
                        operatorUserId,
                        before,
                        BuildLogSnapshot(existing),
                        reviewTaskId);
                }
                else if (!existing.IsDel)
                {
                    var before = BuildLogSnapshot(existing);
                    existing.IsDel = true;
                    existing.Status = (byte)MediaFileStatus.Archived;
                    existing.UpdatedAt = DateTime.UtcNow;

                    await _auditLogService.WriteAsync(
                        "Sys_Media_Files",
                        existing.MediaID,
                        "DELETE",
                        operatorUserId,
                        before,
                        BuildLogSnapshot(existing),
                        reviewTaskId);
                }
            }

            var appendIds = ids.Where(id => !existingMap.ContainsKey(id)).ToList();
            if (appendIds.Count > 0)
            {
                var appendFiles = await GetEntitiesByIdsInternalAsync(appendIds, true);
                foreach (var file in appendFiles)
                {
                    var before = BuildLogSnapshot(file);
                    file.TreeID = normalizedTreeId;
                    file.OwnerType = ownerType;
                    file.OwnerID = ownerId;
                    file.Status = (byte)MediaFileStatus.Approved;
                    file.ReviewTaskID = reviewTaskId;
                    file.IsDel = false;
                    file.UpdatedAt = DateTime.UtcNow;

                    await _auditLogService.WriteAsync(
                        "Sys_Media_Files",
                        file.MediaID,
                        "UPDATE",
                        operatorUserId,
                        before,
                        BuildLogSnapshot(file),
                        reviewTaskId);
                }
            }

            await _db.SaveChangesAsync();
        }

        private async Task<List<SysMediaFile>> GetEntitiesByIdsInternalAsync(IReadOnlyCollection<Guid> mediaIds, bool includeDeleted)
        {
            if (mediaIds.Count == 0)
            {
                return new List<SysMediaFile>();
            }

            var query = includeDeleted
                ? _db.MediaFiles.IgnoreQueryFilters().AsQueryable()
                : _db.MediaFiles.AsQueryable();

            return await BuildGuidPredicateQuery(query, item => item.MediaID, mediaIds)
                .ToListAsync();
        }

        private string GetMediaRootPath()
        {
            return _settings.ResolveRootPath(_environment.ContentRootPath);
        }

        private static IReadOnlyCollection<Guid> DistinctIds(IEnumerable<Guid> ids)
        {
            return ids.Where(id => id != Guid.Empty).Distinct().ToList();
        }

        private static Guid? NormalizeTreeId(Guid? treeId)
        {
            return treeId == Guid.Empty ? null : treeId;
        }

        private static string NormalizeOwnerType(string ownerType)
        {
            return string.IsNullOrWhiteSpace(ownerType) ? "event" : ownerType.Trim().ToLowerInvariant();
        }

        private static string NormalizeRequestPath(string? requestPath)
        {
            var value = string.IsNullOrWhiteSpace(requestPath) ? "/file" : requestPath.Trim();
            if (!value.StartsWith('/'))
            {
                value = "/" + value;
            }

            return value.TrimEnd('/');
        }

        private static MediaFileDto MapDto(SysMediaFile entity)
        {
            return new MediaFileDto
            {
                MediaId = entity.MediaID,
                TreeId = entity.TreeID,
                FileName = entity.FileName,
                FileExt = entity.FileExt,
                MimeType = entity.MimeType,
                FileSize = entity.FileSize,
                PublicUrl = entity.PublicUrl,
                Caption = entity.Caption,
                SortOrder = entity.SortOrder,
                Status = entity.Status,
                StatusName = ReviewActions.GetMediaStatusDisplayName(entity.Status),
                CreatedAt = entity.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        private static object BuildLogSnapshot(SysMediaFile file)
        {
            return new
            {
                mediaId = file.MediaID,
                treeId = file.TreeID,
                ownerType = file.OwnerType,
                ownerId = file.OwnerID,
                file.FileName,
                file.FileExt,
                file.MimeType,
                file.FileSize,
                file.PublicUrl,
                file.Caption,
                file.SortOrder,
                file.ReviewTaskID,
                file.Status,
                statusName = ReviewActions.GetMediaStatusDisplayName(file.Status),
                file.IsDel
            };
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
    }
}



