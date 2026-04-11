using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;
using 家谱.Models.Enums;

namespace 家谱.Services
{
    /// <summary>
    /// 历史事件服务接口。
    /// </summary>
    public interface IGenoEventService
    {
        Task<List<GenoEventViewDto>> GetByTreeIdAsync(Guid treeId, bool includeGlobal = true, bool includePrivateFamily = false, bool includePrivateGlobal = false);

        Task<List<GenoEventViewDto>> GetGlobalAsync(bool includePrivate = false);

        Task<GenoEventViewDto?> GetByIdAsync(Guid eventId);

        Task<GenoEvent> CreateAsync(GenoEventDto dto, Guid operatorUserId, Guid? taskId = null);

        Task<bool> UpdateAsync(Guid eventId, GenoEventDto dto, Guid operatorUserId, Guid? taskId = null);

        Task<bool> DeleteAsync(Guid eventId, Guid operatorUserId, Guid? taskId = null);
    }

    /// <summary>
    /// 历史事件服务。
    /// </summary>
    public class GenoEventService : IGenoEventService
    {
        private readonly GenealogyDbContext _db;
        private readonly IAuditLogService _auditLogService;
        private readonly IMediaFileService _mediaFileService;

        public GenoEventService(
            GenealogyDbContext db,
            IAuditLogService auditLogService,
            IMediaFileService mediaFileService)
        {
            _db = db;
            _auditLogService = auditLogService;
            _mediaFileService = mediaFileService;
        }

        public async Task<List<GenoEventViewDto>> GetByTreeIdAsync(Guid treeId, bool includeGlobal = true, bool includePrivateFamily = false, bool includePrivateGlobal = false)
        {
            var events = await _db.GenoEvents
                .AsNoTracking()
                .Where(item =>
                    (item.TreeID == treeId && !item.IsGlobal && (includePrivateFamily || item.IsPublic)) ||
                    (includeGlobal && item.IsGlobal && (includePrivateGlobal || item.IsPublic)))
                .OrderByDescending(item => item.EventDate ?? item.CreatedAt)
                .ThenByDescending(item => item.CreatedAt)
                .ToListAsync();

            return await BuildViewListAsync(events);
        }

        public async Task<List<GenoEventViewDto>> GetGlobalAsync(bool includePrivate = false)
        {
            var events = await _db.GenoEvents
                .AsNoTracking()
                .Where(item => item.IsGlobal && (includePrivate || item.IsPublic))
                .OrderByDescending(item => item.EventDate ?? item.CreatedAt)
                .ThenByDescending(item => item.CreatedAt)
                .ToListAsync();

            return await BuildViewListAsync(events);
        }

        public async Task<GenoEventViewDto?> GetByIdAsync(Guid eventId)
        {
            var entity = await _db.GenoEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.EventID == eventId);

            if (entity == null)
            {
                return null;
            }

            return (await BuildViewListAsync(new List<GenoEvent> { entity })).FirstOrDefault();
        }

        public async Task<GenoEvent> CreateAsync(GenoEventDto dto, Guid operatorUserId, Guid? taskId = null)
        {
            var normalized = NormalizeDto(dto);

            var entity = new GenoEvent
            {
                EventID = Guid.NewGuid(),
                TreeID = normalized.TreeId,
                EventTitle = normalized.EventTitle.Trim(),
                EventType = normalized.EventType,
                IsGlobal = normalized.IsGlobal,
                IsPublic = normalized.IsPublic,
                EventDate = normalized.EventDate,
                DateRaw = string.IsNullOrWhiteSpace(normalized.DateRaw) ? null : normalized.DateRaw.Trim(),
                LocationID = normalized.LocationId,
                Description = string.IsNullOrWhiteSpace(normalized.Description) ? null : normalized.Description.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDel = false
            };

            _db.GenoEvents.Add(entity);
            await _db.SaveChangesAsync();

            await SyncParticipantsAsync(entity, normalized.Participants, operatorUserId, taskId);
            await _mediaFileService.SyncEventMediaAsync(entity.EventID, entity.TreeID, normalized.MediaIds, operatorUserId, taskId);

            await _auditLogService.WriteAsync(
                "Geno_Events",
                entity.EventID,
                "CREATE",
                operatorUserId,
                new { },
                await BuildEventLogSnapshotAsync(entity),
                taskId);

            return entity;
        }

        public async Task<bool> UpdateAsync(Guid eventId, GenoEventDto dto, Guid operatorUserId, Guid? taskId = null)
        {
            var entity = await _db.GenoEvents
                .FirstOrDefaultAsync(item => item.EventID == eventId);

            if (entity == null)
            {
                return false;
            }

            var before = await BuildEventLogSnapshotAsync(entity);
            var normalized = NormalizeDto(dto);

            entity.TreeID = normalized.TreeId;
            entity.EventTitle = normalized.EventTitle.Trim();
            entity.EventType = normalized.EventType;
            entity.IsGlobal = normalized.IsGlobal;
            entity.IsPublic = normalized.IsPublic;
            entity.EventDate = normalized.EventDate;
            entity.DateRaw = string.IsNullOrWhiteSpace(normalized.DateRaw) ? null : normalized.DateRaw.Trim();
            entity.LocationID = normalized.LocationId;
            entity.Description = string.IsNullOrWhiteSpace(normalized.Description) ? null : normalized.Description.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await SyncParticipantsAsync(entity, normalized.Participants, operatorUserId, taskId);
            await _mediaFileService.SyncEventMediaAsync(entity.EventID, entity.TreeID, normalized.MediaIds, operatorUserId, taskId);

            await _auditLogService.WriteAsync(
                "Geno_Events",
                entity.EventID,
                "UPDATE",
                operatorUserId,
                before,
                await BuildEventLogSnapshotAsync(entity),
                taskId);

            return true;
        }

        public async Task<bool> DeleteAsync(Guid eventId, Guid operatorUserId, Guid? taskId = null)
        {
            var entity = await _db.GenoEvents
                .FirstOrDefaultAsync(item => item.EventID == eventId);

            if (entity == null)
            {
                return false;
            }

            var before = await BuildEventLogSnapshotAsync(entity);

            entity.IsDel = true;
            entity.UpdatedAt = DateTime.UtcNow;

            var participants = await _db.GenoEventParticipants
                .IgnoreQueryFilters()
                .Where(item => item.EventID == eventId && !item.IsDel)
                .ToListAsync();

            var memberIds = participants.Select(item => item.MemberID).Distinct().ToList();
            var members = await LoadMembersAsync(memberIds);
            var memberMap = members.ToDictionary(item => item.MemberID);

            foreach (var participant in participants)
            {
                var beforeParticipant = BuildParticipantLogSnapshot(participant, memberMap.GetValueOrDefault(participant.MemberID), entity);
                participant.IsDel = true;
                participant.UpdatedAt = DateTime.UtcNow;

                await _auditLogService.WriteAsync(
                    "Geno_Event_Participants",
                    participant.MemberID,
                    "DELETE",
                    operatorUserId,
                    beforeParticipant,
                    BuildParticipantLogSnapshot(participant, memberMap.GetValueOrDefault(participant.MemberID), entity),
                    taskId);
            }

            await _db.SaveChangesAsync();
            await _mediaFileService.SoftDeleteByOwnerAsync("event", entity.EventID, operatorUserId, taskId);

            await _auditLogService.WriteAsync(
                "Geno_Events",
                entity.EventID,
                "DELETE",
                operatorUserId,
                before,
                await BuildEventLogSnapshotAsync(entity),
                taskId);

            return true;
        }

        private async Task<List<GenoEventViewDto>> BuildViewListAsync(IReadOnlyCollection<GenoEvent> events)
        {
            if (events.Count == 0)
            {
                return new List<GenoEventViewDto>();
            }

            var eventIds = events.Select(item => item.EventID).Distinct().ToList();
            var treeIds = events.Where(item => item.TreeID.HasValue).Select(item => item.TreeID!.Value).Distinct().ToList();

            var participants = await LoadParticipantsAsync(eventIds);
            var memberIds = participants.Select(item => item.MemberID).Distinct().ToList();
            var members = await LoadMembersAsync(memberIds);
            var memberMap = members.ToDictionary(item => item.MemberID);

            var treeMap = treeIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await BuildGuidPredicateQuery(_db.GenoTrees.IgnoreQueryFilters().AsNoTracking(), item => item.TreeID, treeIds)
                    .ToDictionaryAsync(item => item.TreeID, item => item.TreeName);

            var mediaGroups = await LoadMediaByOwnerAsync(eventIds);

            return events.Select(item =>
            {
                var eventParticipants = participants
                    .Where(participant => participant.EventID == item.EventID)
                    .OrderBy(participant => participant.CreatedAt)
                    .Select(participant => new EventParticipantViewDto
                    {
                        MemberId = participant.MemberID,
                        FullName = memberMap.TryGetValue(participant.MemberID, out var member)
                            ? $"{member.LastName}{member.FirstName}"
                            : "未知成员",
                        RoleDescription = participant.RoleDescription
                    })
                    .ToList();

                return new GenoEventViewDto
                {
                    EventId = item.EventID,
                    TreeId = item.TreeID,
                    TreeName = item.TreeID.HasValue && treeMap.TryGetValue(item.TreeID.Value, out var treeName) ? treeName : null,
                    EventTitle = item.EventTitle,
                    EventType = item.EventType,
                    EventTypeName = ReviewActions.GetEventTypeDisplayName(item.EventType),
                    IsGlobal = item.IsGlobal,
                    IsPublic = item.IsPublic,
                    EventDate = item.EventDate,
                    DateRaw = item.DateRaw,
                    LocationId = item.LocationID,
                    Description = item.Description,
                    Participants = eventParticipants,
                    MediaFiles = mediaGroups.TryGetValue(item.EventID, out var media) ? media : new List<MediaFileDto>()
                };
            }).ToList();
        }

        private async Task<List<GenoEventParticipant>> LoadParticipantsAsync(IReadOnlyCollection<Guid> eventIds)
        {
            if (eventIds.Count == 0)
            {
                return new List<GenoEventParticipant>();
            }

            return await BuildGuidPredicateQuery(_db.GenoEventParticipants.AsNoTracking(), item => item.EventID, eventIds)
                .OrderBy(item => item.CreatedAt)
                .ToListAsync();
        }

        private async Task<List<GenoMember>> LoadMembersAsync(IReadOnlyCollection<Guid> memberIds)
        {
            if (memberIds.Count == 0)
            {
                return new List<GenoMember>();
            }

            return await BuildGuidPredicateQuery(_db.GenoMembers.IgnoreQueryFilters().AsNoTracking(), item => item.MemberID, memberIds)
                .Where(item => item.IsDel != true)
                .ToListAsync();
        }

        private async Task<Dictionary<Guid, List<MediaFileDto>>> LoadMediaByOwnerAsync(IReadOnlyCollection<Guid> eventIds)
        {
            var result = new Dictionary<Guid, List<MediaFileDto>>();
            if (eventIds.Count == 0)
            {
                return result;
            }

            var mediaQuery = _db.MediaFiles
                .AsNoTracking()
                .Where(item => item.OwnerType == "event" && item.OwnerID.HasValue && item.Status == (byte)MediaFileStatus.Approved);

            var mediaFiles = await BuildGuidPredicateQuery(mediaQuery, item => item.OwnerID!.Value, eventIds)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.CreatedAt)
                .ToListAsync();

            foreach (var media in mediaFiles)
            {
                if (!media.OwnerID.HasValue)
                {
                    continue;
                }

                if (!result.TryGetValue(media.OwnerID.Value, out var list))
                {
                    list = new List<MediaFileDto>();
                    result[media.OwnerID.Value] = list;
                }

                list.Add(new MediaFileDto
                {
                    MediaId = media.MediaID,
                    TreeId = media.TreeID,
                    FileName = media.FileName,
                    FileExt = media.FileExt,
                    MimeType = media.MimeType,
                    FileSize = media.FileSize,
                    PublicUrl = media.PublicUrl,
                    Caption = media.Caption,
                    SortOrder = media.SortOrder,
                    Status = media.Status,
                    StatusName = ReviewActions.GetMediaStatusDisplayName(media.Status),
                    CreatedAt = media.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return result;
        }

        private async Task SyncParticipantsAsync(GenoEvent entity, IReadOnlyCollection<EventParticipantDto> participants, Guid operatorUserId, Guid? taskId)
        {
            var normalizedParticipants = participants
                .Where(item => item.MemberId != Guid.Empty)
                .GroupBy(item => item.MemberId)
                .Select(group => group.Last())
                .ToList();

            var memberIds = normalizedParticipants.Select(item => item.MemberId).Distinct().ToList();
            var members = await LoadMembersAsync(memberIds);
            var memberMap = members.ToDictionary(item => item.MemberID);

            if (memberIds.Count != members.Count)
            {
                throw new KeyNotFoundException("存在未找到的事件参与成员");
            }

            if (entity.TreeID.HasValue && members.Any(item => item.TreeID != entity.TreeID.Value))
            {
                throw new InvalidOperationException("树内历史事件的参与成员必须属于同一棵家谱树");
            }

            var existing = await _db.GenoEventParticipants
                .IgnoreQueryFilters()
                .Where(item => item.EventID == entity.EventID)
                .ToListAsync();

            var existingMap = existing.ToDictionary(item => item.MemberID);

            foreach (var participant in normalizedParticipants)
            {
                var roleDescription = string.IsNullOrWhiteSpace(participant.RoleDescription)
                    ? null
                    : participant.RoleDescription.Trim();

                if (existingMap.TryGetValue(participant.MemberId, out var entityParticipant))
                {
                    var before = BuildParticipantLogSnapshot(entityParticipant, memberMap[participant.MemberId], entity);
                    entityParticipant.RoleDescription = roleDescription;
                    entityParticipant.IsDel = false;
                    entityParticipant.UpdatedAt = DateTime.UtcNow;

                    await _auditLogService.WriteAsync(
                        "Geno_Event_Participants",
                        entityParticipant.MemberID,
                        "UPDATE",
                        operatorUserId,
                        before,
                        BuildParticipantLogSnapshot(entityParticipant, memberMap[participant.MemberId], entity),
                        taskId);
                }
                else
                {
                    entityParticipant = new GenoEventParticipant
                    {
                        EventID = entity.EventID,
                        MemberID = participant.MemberId,
                        RoleDescription = roleDescription,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsDel = false
                    };

                    _db.GenoEventParticipants.Add(entityParticipant);

                    await _auditLogService.WriteAsync(
                        "Geno_Event_Participants",
                        entityParticipant.MemberID,
                        "CREATE",
                        operatorUserId,
                        new { },
                        BuildParticipantLogSnapshot(entityParticipant, memberMap[participant.MemberId], entity),
                        taskId);
                }
            }

            foreach (var participant in existing.Where(item => !normalizedParticipants.Any(input => input.MemberId == item.MemberID) && !item.IsDel))
            {
                var before = BuildParticipantLogSnapshot(participant, memberMap.GetValueOrDefault(participant.MemberID), entity);
                participant.IsDel = true;
                participant.UpdatedAt = DateTime.UtcNow;

                await _auditLogService.WriteAsync(
                    "Geno_Event_Participants",
                    participant.MemberID,
                    "DELETE",
                    operatorUserId,
                    before,
                    BuildParticipantLogSnapshot(participant, memberMap.GetValueOrDefault(participant.MemberID), entity),
                    taskId);
            }

            await _db.SaveChangesAsync();
        }

        private async Task<object> BuildEventLogSnapshotAsync(GenoEvent entity)
        {
            var view = await GetByIdAsync(entity.EventID);
            var participantSummary = view == null
                ? string.Empty
                : string.Join("；", view.Participants.Select(item => string.IsNullOrWhiteSpace(item.RoleDescription)
                    ? item.FullName
                    : $"{item.FullName}（{item.RoleDescription}）"));

            var mediaSummary = view == null
                ? string.Empty
                : string.Join("；", view.MediaFiles.Select(BuildMediaDisplayText));

            return new
            {
                eventId = entity.EventID,
                treeId = entity.TreeID,
                eventTitle = entity.EventTitle,
                eventType = entity.EventType,
                eventTypeName = ReviewActions.GetEventTypeDisplayName(entity.EventType),
                entity.IsGlobal,
                entity.IsPublic,
                eventDate = entity.EventDate,
                entity.DateRaw,
                locationId = entity.LocationID,
                entity.Description,
                participantSummary,
                participantCount = view?.Participants.Count ?? 0,
                mediaSummary,
                mediaCount = view?.MediaFiles.Count ?? 0,
                entity.IsDel
            };
        }

        private static object BuildParticipantLogSnapshot(GenoEventParticipant participant, GenoMember? member, GenoEvent entity)
        {
            return new
            {
                eventId = participant.EventID,
                eventTitle = entity.EventTitle,
                memberId = participant.MemberID,
                memberName = member == null ? "未知成员" : $"{member.LastName}{member.FirstName}",
                participant.RoleDescription,
                participant.IsDel
            };
        }

        private static string BuildMediaDisplayText(MediaFileDto item)
        {
            if (!string.IsNullOrWhiteSpace(item.Caption))
            {
                return item.Caption!;
            }

            var mimeType = (item.MimeType ?? string.Empty).ToLowerInvariant();
            var fileExt = (item.FileExt ?? string.Empty).ToLowerInvariant();
            if (mimeType.StartsWith("image/") || fileExt is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp")
            {
                return "图片资料";
            }

            if (mimeType.StartsWith("video/") || fileExt is ".mp4" or ".mov" or ".avi" or ".webm" or ".m4v")
            {
                return "视频资料";
            }

            return "文档资料";
        }

        private static GenoEventDto NormalizeDto(GenoEventDto dto)
        {
            var treeId = dto.TreeId == Guid.Empty ? null : dto.TreeId;
            if (!dto.IsGlobal && !treeId.HasValue)
            {
                throw new InvalidOperationException("树内历史事件必须指定所属家谱树");
            }

            return new GenoEventDto
            {
                TreeId = dto.IsGlobal ? null : treeId,
                EventTitle = dto.EventTitle,
                EventType = dto.EventType,
                IsGlobal = dto.IsGlobal,
                IsPublic = dto.IsPublic,
                EventDate = dto.EventDate,
                DateRaw = dto.DateRaw,
                LocationId = dto.LocationId,
                Description = dto.Description,
                Participants = dto.Participants ?? new List<EventParticipantDto>(),
                MediaIds = dto.MediaIds ?? new List<Guid>()
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
