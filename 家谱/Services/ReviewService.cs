using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 家谱.Models.DTOs.Common;
using 家谱.Common;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;
using 家谱.Models.Enums;
using 家谱.Services.Common;

namespace 家谱.Services
{
    /// <summary>
    /// 审核服务接口。
    /// </summary>
    public interface IReviewService
    {
        Task<Guid> SubmitAsync(SubmitReviewRequest dto, Guid submitterId);

        Task<WorkflowResultDto> ProcessAsync(TaskProcessDto dto, Guid reviewerId);

        Task<List<TaskDtos>> GetTaskList(Guid userId);

        Task<PagedResult<TaskDtos>> QueryMySubmissionsAsync(Guid userId, int page = 1, int pageSize = 20, byte? status = null);

        Task<PagedResult<TaskDtos>> QueryReviewHistoryAsync(Guid userId, int page = 1, int pageSize = 20, byte? status = null);

        Task<List<TaskDtos>> GetAll(Guid userId);
    }

    /// <summary>
    /// 审核服务。
    /// </summary>
    public class ReviewService : IReviewService
    {
        private readonly GenealogyDbContext _db;
        private readonly IHandleTasks _handler;
        private readonly ITreePermissionService _treePermissionService;
        private readonly IAuditLogService _auditLogService;
        private readonly IMediaFileService _mediaFileService;

        public ReviewService(
            GenealogyDbContext db,
            IHandleTasks handleTasks,
            ITreePermissionService treePermissionService,
            IAuditLogService auditLogService,
            IMediaFileService mediaFileService)
        {
            _db = db;
            _handler = handleTasks;
            _treePermissionService = treePermissionService;
            _auditLogService = auditLogService;
            _mediaFileService = mediaFileService;
        }

        /// <summary>
        /// 提交审核任务。
        /// </summary>
        public async Task<Guid> SubmitAsync(SubmitReviewRequest dto, Guid submitterId)
        {
            IQueryable<ReviewTask> duplicateQuery = _db.ReviewTasks.Where(task =>
                task.SubmitterID == submitterId &&
                task.ActionCode == dto.ActionCode &&
                task.Status == (byte)ReviewStatus.Pending);

            if (dto.TargetId.HasValue)
            {
                duplicateQuery = duplicateQuery.Where(task => task.TargetID == dto.TargetId);
            }

            if (dto.TreeId.HasValue)
            {
                duplicateQuery = duplicateQuery.Where(task => task.TreeID == dto.TreeId);
            }

            if (!dto.ForceCreateTask && await duplicateQuery.AnyAsync())
            {
                throw new InvalidOperationException("你已提交过同类型待审核申请，请等待审核结果");
            }

            var task = new ReviewTask
            {
                TreeID = dto.TreeId,
                SubmitterID = submitterId,
                ActionCode = dto.ActionCode,
                TargetID = dto.TargetId,
                ChangeData = dto.ChangeData,
                ApplyReason = dto.Reason,
                Status = (byte)ReviewStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.ReviewTasks.Add(task);
            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Sys_Review_Tasks",
                task.TaskID,
                "CREATE",
                submitterId,
                new { },
                BuildTaskLogSnapshot(task),
                task.TaskID);

            return task.TaskID;
        }

        /// <summary>
        /// 处理审核任务。
        /// </summary>
        public async Task<WorkflowResultDto> ProcessAsync(TaskProcessDto dto, Guid reviewerId)
        {
            if (dto.TaskId == Guid.Empty)
            {
                throw new ArgumentException("审核任务不能为空");
            }

            if (dto.Action is not (int)ReviewProcessAction.Approve and not (int)ReviewProcessAction.Reject)
            {
                throw new ArgumentException("无效的审核操作");
            }

            var task = await _db.ReviewTasks.FirstOrDefaultAsync(item => item.TaskID == dto.TaskId);
            if (task == null || task.Status != (byte)ReviewStatus.Pending)
            {
                throw new InvalidOperationException("任务不存在或已处理");
            }

            if (!await _treePermissionService.CanReviewTaskAsync(task, reviewerId))
            {
                throw new UnauthorizedAccessException("无权处理该审核任务");
            }

            var before = BuildTaskLogSnapshot(task);

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                if (dto.Action == (int)ReviewProcessAction.Approve)
                {
                    await ExecuteApprovedTaskAsync(task, reviewerId);
                    task.Status = (byte)ReviewStatus.Approved;
                }
                else
                {
                    task.Status = (byte)ReviewStatus.Rejected;
                    if (task.ActionCode is ReviewActions.EventCreate or ReviewActions.EventUpdate)
                    {
                        await _mediaFileService.MarkRejectedAsync(task.TaskID, reviewerId);
                    }
                }

                task.ReviewerID = reviewerId;
                task.ReviewNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
                task.ProcessedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                await _auditLogService.WriteAsync(
                    "Sys_Review_Tasks",
                    task.TaskID,
                    "UPDATE",
                    reviewerId,
                    before,
                    BuildTaskLogSnapshot(task),
                    task.TaskID);

                await transaction.CommitAsync();

                return new WorkflowResultDto
                {
                    Message = dto.Action == (int)ReviewProcessAction.Approve ? "审核通过并已执行" : "审核已驳回",
                    Data = new
                    {
                        taskId = task.TaskID,
                        status = task.Status,
                        processedAt = task.ProcessedAt
                    }
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// 获取当前用户提交或处理过的任务。
        /// </summary>
        public async Task<List<TaskDtos>> GetTaskList(Guid userId)
        {
            var userExists = await _db.Users.AnyAsync(user => user.UserID == userId && user.UserStatus == 1);
            if (!userExists)
            {
                throw new KeyNotFoundException("用户不存在");
            }

            var tasks = await _db.ReviewTasks
                .AsNoTracking()
                .Where(task => task.SubmitterID == userId || task.ReviewerID == userId)
                .OrderByDescending(task => task.CreatedAt)
                .ToListAsync();

            var result = new List<TaskDtos>(tasks.Count);
            foreach (var task in tasks)
            {
                result.Add(await MapTaskDtoAsync(task, userId));
            }

            return result;
        }

        /// <summary>
        /// 获取当前用户可处理的待审核任务。
        /// </summary>
        public async Task<PagedResult<TaskDtos>> QueryMySubmissionsAsync(Guid userId, int page = 1, int pageSize = 20, byte? status = null)
        {
            var userExists = await _db.Users.AnyAsync(user => user.UserID == userId && user.UserStatus == 1);
            if (!userExists)
            {
                throw new KeyNotFoundException("用户不存在");
            }

            var currentPage = Math.Max(page, 1);
            var currentPageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.ReviewTasks
                .AsNoTracking()
                .Where(task => task.SubmitterID == userId);

            if (status.HasValue)
            {
                query = query.Where(task => task.Status == status.Value);
            }

            var totalCount = await query.CountAsync();
            var tasks = await query
                .OrderByDescending(task => task.CreatedAt)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToListAsync();

            var items = new List<TaskDtos>(tasks.Count);
            foreach (var task in tasks)
            {
                items.Add(await MapTaskDtoAsync(task, userId));
            }

            return new PagedResult<TaskDtos>
            {
                Items = items,
                Page = currentPage,
                PageSize = currentPageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)currentPageSize)
            };
        }

        public async Task<PagedResult<TaskDtos>> QueryReviewHistoryAsync(Guid userId, int page = 1, int pageSize = 20, byte? status = null)
        {
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserID == userId && item.UserStatus == 1);

            if (user == null)
            {
                throw new KeyNotFoundException("当前用户不存在");
            }

            var currentPage = Math.Max(page, 1);
            var currentPageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.ReviewTasks
                .AsNoTracking()
                .Where(task => task.ReviewerID == userId && task.Status != (byte)ReviewStatus.Pending);

            if (status.HasValue)
            {
                query = query.Where(task => task.Status == status.Value);
            }

            var totalCount = await query.CountAsync();
            var tasks = await query
                .OrderByDescending(task => task.ProcessedAt ?? task.CreatedAt)
                .ThenByDescending(task => task.CreatedAt)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToListAsync();

            var items = new List<TaskDtos>(tasks.Count);
            foreach (var task in tasks)
            {
                items.Add(await MapTaskDtoAsync(task, userId));
            }

            return new PagedResult<TaskDtos>
            {
                Items = items,
                Page = currentPage,
                PageSize = currentPageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)currentPageSize)
            };
        }

        public async Task<List<TaskDtos>> GetAll(Guid userId)
        {
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserID == userId && item.UserStatus == 1);

            if (user == null)
            {
                throw new KeyNotFoundException("用户不存在");
            }

            var tasks = await _db.ReviewTasks
                .AsNoTracking()
                .Where(task => task.Status == (byte)ReviewStatus.Pending)
                .OrderByDescending(task => task.CreatedAt)
                .ToListAsync();

            var result = new List<TaskDtos>();
            foreach (var task in tasks)
            {
                if (!await _treePermissionService.CanReviewTaskAsync(task, userId))
                {
                    continue;
                }

                result.Add(await MapTaskDtoAsync(task, userId, true));
            }

            return result;
        }

        private async Task ExecuteApprovedTaskAsync(ReviewTask task, Guid reviewerId)
        {
            switch (task.ActionCode)
            {
                case ReviewActions.ApplyAdmin:
                    await _handler.HandleApplyAdminAsync(task, reviewerId);
                    break;
                case ReviewActions.TreeApplyRole:
                    await _handler.HandleTreeApplyRoleAsync(task, reviewerId);
                    break;
                case ReviewActions.TreeCreate:
                    await _handler.HandleTreeCreateAsync(task, reviewerId);
                    break;
                case ReviewActions.TreeUpdate:
                    await _handler.HandleTreeUpdateAsync(task, reviewerId);
                    break;
                case ReviewActions.TreeDelete:
                    await _handler.HandleTreeDeleteAsync(task, reviewerId);
                    break;
                case ReviewActions.PoemCreate:
                    await _handler.HandlePoemCreateAsync(task, reviewerId);
                    break;
                case ReviewActions.PoemUpdate:
                    await _handler.HandlePoemUpdateAsync(task, reviewerId);
                    break;
                case ReviewActions.PoemDelete:
                    await _handler.HandlePoemDeleteAsync(task, reviewerId);
                    break;
                case ReviewActions.MemberCreate:
                    await _handler.HandleMemberCreateAsync(task, reviewerId);
                    break;
                case ReviewActions.MemberUpdate:
                    await _handler.HandleMemberUpdateAsync(task, reviewerId);
                    break;
                case ReviewActions.MemberDelete:
                    await _handler.HandleMemberDeleteAsync(task, reviewerId);
                    break;
                case ReviewActions.MemberIdentify:
                    await _handler.HandleMemberIdentifyAsync(task, reviewerId);
                    break;
                case ReviewActions.UnionCreate:
                    await _handler.HandleUnionCreateAsync(task, reviewerId);
                    break;
                case ReviewActions.UnionDelete:
                    await _handler.HandleUnionDeleteAsync(task, reviewerId);
                    break;
                case ReviewActions.UnionMemberAdd:
                    await _handler.HandleUnionMemberAddAsync(task, reviewerId);
                    break;
                case ReviewActions.UnionMemberDelete:
                    await _handler.HandleUnionMemberDeleteAsync(task, reviewerId);
                    break;
                case ReviewActions.EventCreate:
                    await _handler.HandleEventCreateAsync(task, reviewerId);
                    break;
                case ReviewActions.EventUpdate:
                    await _handler.HandleEventUpdateAsync(task, reviewerId);
                    break;
                case ReviewActions.EventDelete:
                    await _handler.HandleEventDeleteAsync(task, reviewerId);
                    break;
                default:
                    throw new InvalidOperationException($"未定义的业务操作：{task.ActionCode}");
            }
        }

        private async Task<TaskDtos> MapTaskDtoAsync(ReviewTask task, Guid currentUserId, bool? canProcessOverride = null)
        {
            var changeData = DeserializeJson(task.ChangeData);
            var submitter = await BuildUserSummaryAsync(task.SubmitterID);
            var reviewer = task.ReviewerID.HasValue ? await BuildUserSummaryAsync(task.ReviewerID.Value) : null;
            var treeSummary = task.TreeID.HasValue
                ? await BuildTreeSummaryAsync(task.TreeID.Value) ?? BuildTreeSummaryFromPayload(task.ActionCode, changeData)
                : BuildTreeSummaryFromPayload(task.ActionCode, changeData);
            var targetSummary = await BuildTargetSummaryAsync(task, changeData);
            var canProcess = canProcessOverride ?? await _treePermissionService.CanReviewTaskAsync(task, currentUserId);

            return new TaskDtos
            {
                ActionCode = task.ActionCode,
                TaskId = task.TaskID,
                SubmitterName = ReadStringProperty(submitter, "username", "未知用户"),
                SubmitterId = task.SubmitterID,
                Submitter = submitter,
                ActionName = ReviewActions.GetDisplayName(task.ActionCode),
                ChangeData = changeData ?? new { },
                TreeId = task.TreeID,
                TreeSummary = treeSummary,
                TargetId = task.TargetID,
                TargetType = GetTargetType(task.ActionCode),
                TargetSummary = targetSummary,
                Reason = task.ApplyReason,
                Status = task.Status switch
                {
                    (byte)ReviewStatus.Pending => "待审核",
                    (byte)ReviewStatus.Approved => "审核通过",
                    (byte)ReviewStatus.Rejected => "审核驳回",
                    _ => "已撤回"
                },
                ReviewName = ReadStringProperty(reviewer, "username", "待处理"),
                ReviewerId = task.ReviewerID,
                Reviewer = reviewer,
                ReviewNotes = task.ReviewNotes ?? string.Empty,
                CreateTime = task.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                ProcessTime = task.ProcessedAt?.ToString("yyyy-MM-dd HH:mm") ?? "待处理",
                CanProcess = canProcess,
                IsHistory = task.Status != (byte)ReviewStatus.Pending
            };
        }

        private async Task<object?> BuildTargetSummaryAsync(ReviewTask task, object? changeData)
        {
            return task.ActionCode switch
            {
                ReviewActions.ApplyAdmin or ReviewActions.TreeApplyRole => await BuildUserSummaryAsync(task.TargetID),
                ReviewActions.TreeCreate => BuildTreeSummaryFromPayload(task.ActionCode, changeData),
                ReviewActions.TreeUpdate => await BuildTreeSummaryAsync(task.TargetID ?? task.TreeID) ?? BuildTreeSummaryFromPayload(task.ActionCode, changeData),
                ReviewActions.TreeDelete => BuildTreeSummaryFromPayload(task.ActionCode, changeData) ?? await BuildTreeSummaryAsync(task.TargetID ?? task.TreeID),
                ReviewActions.PoemCreate => BuildPoemSummaryFromPayload(changeData),
                ReviewActions.PoemUpdate => await BuildPoemSummaryAsync(task.TargetID) ?? BuildPoemSummaryFromPayload(changeData),
                ReviewActions.PoemDelete => BuildPoemSummaryFromPayload(changeData) ?? await BuildPoemSummaryAsync(task.TargetID),
                ReviewActions.MemberCreate or ReviewActions.MemberUpdate or ReviewActions.MemberDelete or ReviewActions.MemberIdentify
                    => await BuildMemberSummaryAsync(task.TargetID) ?? BuildMemberSummaryFromPayload(changeData),
                ReviewActions.UnionCreate => BuildUnionSummaryFromPayload(changeData),
                ReviewActions.UnionDelete => await BuildUnionSummaryAsync(task.TargetID) ?? BuildUnionSummaryFromPayload(changeData),
                ReviewActions.UnionMemberAdd or ReviewActions.UnionMemberDelete => await BuildMemberSummaryAsync(task.TargetID) ?? BuildMemberSummaryFromPayload(changeData),
                ReviewActions.EventCreate => await BuildEventSummaryFromPayloadAsync(changeData),
                ReviewActions.EventUpdate => await BuildEventSummaryAsync(task.TargetID) ?? await BuildEventSummaryFromPayloadAsync(changeData),
                ReviewActions.EventDelete => await BuildEventSummaryAsync(task.TargetID) ?? await BuildEventSummaryFromPayloadAsync(changeData),
                _ => null
            };
        }

        private async Task<object?> BuildUserSummaryAsync(Guid? userId)
        {
            if (userId == null)
            {
                return null;
            }

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserID == userId.Value && item.UserStatus == 1);

            if (user == null)
            {
                return null;
            }

            return new
            {
                userId = user.UserID,
                username = user.Username,
                roleType = user.RoleType,
                roleName = ReviewActions.GetRoleDisplayName(user.RoleType),
                email = user.Email,
                phone = user.Phone
            };
        }

        private async Task<object?> BuildTreeSummaryAsync(Guid? treeId)
        {
            if (treeId == null)
            {
                return null;
            }

            var tree = await _db.GenoTrees
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TreeID == treeId.Value);

            if (tree == null)
            {
                return null;
            }

            return new
            {
                treeId = tree.TreeID,
                treeName = tree.TreeName,
                ancestorName = tree.AncestorName,
                region = tree.Region,
                description = tree.Description,
                isPublic = tree.IsPublic,
                isDeleted = tree.IsDel
            };
        }

        private static object? BuildTreeSummaryFromPayload(string actionCode, object? payload)
        {
            if (payload == null || actionCode is not (ReviewActions.TreeCreate or ReviewActions.TreeUpdate or ReviewActions.TreeDelete))
            {
                return null;
            }

            var element = ToJsonElement(payload);
            if (element == null)
            {
                return null;
            }

            return new
            {
                treeId = ReadGuid(element.Value, "treeId", "TreeID"),
                treeName = ReadString(element.Value, "treeName", "TreeName"),
                ancestorName = ReadString(element.Value, "ancestorName", "AncestorName"),
                region = ReadString(element.Value, "region", "Region"),
                description = ReadString(element.Value, "description", "Description"),
                isPublic = ReadBool(element.Value, "isPublic", "IsPublic"),
                isDeleted = ReadBool(element.Value, "isDel", "IsDel")
            };
        }

        private async Task<object?> BuildPoemSummaryAsync(Guid? poemId)
        {
            if (poemId == null)
            {
                return null;
            }

            var poem = await _db.GenoGenerationPoems
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.PoemID == poemId.Value);

            if (poem == null)
            {
                return null;
            }

            return new
            {
                poemId = poem.PoemID,
                treeId = poem.TreeID,
                generationNum = poem.GenerationNum,
                word = poem.Word,
                meaning = poem.Meaning,
                isDeleted = poem.IsDel
            };
        }

        private static object? BuildPoemSummaryFromPayload(object? payload)
        {
            var element = ToJsonElement(payload);
            if (element == null)
            {
                return null;
            }

            return new
            {
                poemId = ReadGuid(element.Value, "poemId", "PoemID"),
                treeId = ReadGuid(element.Value, "treeId", "TreeID"),
                generationNum = ReadInt(element.Value, "generationNum", "GenerationNum"),
                word = ReadString(element.Value, "word", "Word"),
                meaning = ReadString(element.Value, "meaning", "Meaning"),
                isDeleted = ReadBool(element.Value, "isDel", "IsDel")
            };
        }

        private async Task<object?> BuildMemberSummaryAsync(Guid? memberId)
        {
            if (memberId == null)
            {
                return null;
            }

            var member = await _db.GenoMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.MemberID == memberId.Value);

            if (member == null)
            {
                return null;
            }

            return new
            {
                memberId = member.MemberID,
                treeId = member.TreeID,
                fullName = $"{member.LastName}{member.FirstName}",
                firstName = member.FirstName,
                lastName = member.LastName,
                generationNum = member.GenerationNum,
                gender = member.Gender,
                birthDateRaw = member.BirthDateRaw,
                biography = member.Biography,
                sysUserId = member.SysUserId,
                isDeleted = member.IsDel
            };
        }

        private static object? BuildMemberSummaryFromPayload(object? payload)
        {
            var element = ToJsonElement(payload);
            if (element == null)
            {
                return null;
            }

            return new
            {
                memberId = ReadGuid(element.Value, "memberId", "MemberID"),
                treeId = ReadGuid(element.Value, "treeId", "TreeID"),
                fullName = $"{ReadString(element.Value, "lastName", "LastName") ?? string.Empty}{ReadString(element.Value, "firstName", "FirstName") ?? string.Empty}",
                firstName = ReadString(element.Value, "firstName", "FirstName"),
                lastName = ReadString(element.Value, "lastName", "LastName"),
                generationNum = ReadInt(element.Value, "generationNum", "GenerationNum"),
                gender = ReadInt(element.Value, "gender", "Gender"),
                birthDateRaw = ReadString(element.Value, "birthDateRaw", "BirthDateRaw"),
                biography = ReadString(element.Value, "biography", "Biography"),
                sysUserId = ReadGuid(element.Value, "sysUserId", "SysUserId"),
                isDeleted = ReadBool(element.Value, "isDel", "IsDel")
            };
        }

        private async Task<object?> BuildUnionSummaryAsync(Guid? unionId)
        {
            if (unionId == null)
            {
                return null;
            }

            var union = await _db.GenoUnions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UnionID == unionId.Value);

            if (union == null)
            {
                return null;
            }

            var partners = await _db.GenoMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(member => member.MemberID == union.Partner1ID || member.MemberID == union.Partner2ID)
                .ToListAsync();

            var partner1 = partners.FirstOrDefault(member => member.MemberID == union.Partner1ID);
            var partner2 = partners.FirstOrDefault(member => member.MemberID == union.Partner2ID);

            return new
            {
                unionId = union.UnionID,
                treeId = partner1?.TreeID ?? partner2?.TreeID,
                partner1Id = union.Partner1ID,
                partner1Name = partner1 == null ? null : $"{partner1.LastName}{partner1.FirstName}",
                partner2Id = union.Partner2ID,
                partner2Name = partner2 == null ? null : $"{partner2.LastName}{partner2.FirstName}",
                unionType = union.UnionType,
                unionTypeName = ReviewActions.GetUnionTypeDisplayName(union.UnionType),
                sortOrder = union.SortOrder,
                marriageDate = union.MarriageDate,
                isDeleted = union.IsDel
            };
        }

        private static object? BuildUnionSummaryFromPayload(object? payload)
        {
            var element = ToJsonElement(payload);
            if (element == null)
            {
                return null;
            }

            return new
            {
                unionId = ReadGuid(element.Value, "unionId", "UnionId"),
                treeId = ReadGuid(element.Value, "treeId", "TreeId"),
                partner1Id = ReadGuid(element.Value, "partner1Id", "Partner1Id"),
                partner1Name = ReadString(element.Value, "partner1Name", "Partner1Name"),
                partner2Id = ReadGuid(element.Value, "partner2Id", "Partner2Id"),
                partner2Name = ReadString(element.Value, "partner2Name", "Partner2Name"),
                unionType = ReadInt(element.Value, "unionType", "UnionType"),
                unionTypeName = ReadString(element.Value, "unionTypeName", "UnionTypeName"),
                sortOrder = ReadInt(element.Value, "sortOrder", "SortOrder"),
                marriageDate = ReadString(element.Value, "marriageDate", "MarriageDate"),
                relType = ReadInt(element.Value, "relType", "RelType"),
                relTypeName = ReadString(element.Value, "relTypeName", "RelTypeName"),
                childOrder = ReadInt(element.Value, "childOrder", "ChildOrder"),
                childName = ReadString(element.Value, "childName", "ChildName")
            };
        }

        private async Task<object?> BuildEventSummaryAsync(Guid? eventId)
        {
            if (eventId == null)
            {
                return null;
            }

            var entity = await _db.GenoEvents
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.EventID == eventId.Value);

            if (entity == null)
            {
                return null;
            }

            var participants = await _db.GenoEventParticipants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.EventID == entity.EventID && !item.IsDel)
                .ToListAsync();

            var participantSummary = string.Empty;
            if (participants.Count > 0)
            {
                var memberIds = participants.Select(item => item.MemberID).Distinct().ToList();
                var members = await BuildGuidPredicateQuery(
                        _db.GenoMembers.IgnoreQueryFilters().AsNoTracking(),
                        item => item.MemberID,
                        memberIds)
                    .ToListAsync();

                var memberMap = members.ToDictionary(item => item.MemberID);
                participantSummary = string.Join("；", participants.Select(item =>
                {
                    var name = memberMap.TryGetValue(item.MemberID, out var member)
                        ? $"{member.LastName}{member.FirstName}"
                        : "未知成员";
                    return string.IsNullOrWhiteSpace(item.RoleDescription) ? name : $"{name}（{item.RoleDescription}）";
                }));
            }

            var mediaFiles = await _db.MediaFiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.OwnerType == "event" && item.OwnerID == entity.EventID && !item.IsDel)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.CreatedAt)
                .ToListAsync();

            var mediaSummary = string.Join("；", mediaFiles.Select(BuildMediaDisplayText));

            return new
            {
                eventId = entity.EventID,
                treeId = entity.TreeID,
                eventTitle = entity.EventTitle,
                eventType = entity.EventType,
                eventTypeName = ReviewActions.GetEventTypeDisplayName(entity.EventType),
                entity.IsGlobal,
                eventDate = entity.EventDate,
                entity.DateRaw,
                locationId = entity.LocationID,
                entity.Description,
                participantSummary,
                participantCount = participants.Count,
                mediaSummary,
                mediaCount = mediaFiles.Count,
                isDeleted = entity.IsDel
            };
        }

        private async Task<object?> BuildEventSummaryFromPayloadAsync(object? payload)
        {
            var element = ToJsonElement(payload);
            if (element == null)
            {
                return null;
            }

            var participantIds = ReadGuidList(element.Value, "participants", "Participants", "memberId", "MemberId");
            var mediaIds = ReadGuidList(element.Value, "mediaIds", "MediaIds");

            string participantSummary = string.Empty;
            if (participantIds.Count > 0)
            {
                var members = await BuildGuidPredicateQuery(
                        _db.GenoMembers.IgnoreQueryFilters().AsNoTracking(),
                        item => item.MemberID,
                        participantIds)
                    .ToListAsync();

                participantSummary = string.Join("；", members.Select(item => $"{item.LastName}{item.FirstName}"));
            }

            string mediaSummary = string.Empty;
            if (mediaIds.Count > 0)
            {
                var mediaFiles = await BuildGuidPredicateQuery(
                        _db.MediaFiles.IgnoreQueryFilters().AsNoTracking(),
                        item => item.MediaID,
                        mediaIds)
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.CreatedAt)
                    .ToListAsync();

                mediaSummary = string.Join("；", mediaFiles.Select(BuildMediaDisplayText));
            }

            return new
            {
                eventId = ReadGuid(element.Value, "eventId", "EventID"),
                treeId = ReadGuid(element.Value, "treeId", "TreeID"),
                eventTitle = ReadString(element.Value, "eventTitle", "EventTitle"),
                eventType = ReadInt(element.Value, "eventType", "EventType"),
                eventTypeName = ReviewActions.GetEventTypeDisplayName((byte)(ReadInt(element.Value, "eventType", "EventType") ?? 0)),
                isGlobal = ReadBool(element.Value, "isGlobal", "IsGlobal"),
                eventDate = ReadString(element.Value, "eventDate", "EventDate"),
                dateRaw = ReadString(element.Value, "dateRaw", "DateRaw"),
                locationId = ReadGuid(element.Value, "locationId", "LocationID"),
                description = ReadString(element.Value, "description", "Description"),
                participantSummary,
                participantCount = participantIds.Count,
                mediaSummary,
                mediaCount = mediaIds.Count,
                isDeleted = ReadBool(element.Value, "isDel", "IsDel")
            };
        }

        private static string BuildMediaDisplayText(SysMediaFile item)
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

        private static object BuildTaskLogSnapshot(ReviewTask task) => new
        {
            taskId = task.TaskID,
            treeId = task.TreeID,
            actionCode = task.ActionCode,
            actionName = ReviewActions.GetDisplayName(task.ActionCode),
            changeData = DeserializeJson(task.ChangeData) ?? new { },
            status = task.Status,
            reviewNotes = task.ReviewNotes,
            applyReason = task.ApplyReason,
            createdAt = task.CreatedAt,
            processedAt = task.ProcessedAt
        };

        private static object? DeserializeJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<object>(json, JsonDefaults.Options);
            }
            catch
            {
                return json;
            }
        }

        private static JsonElement? ToJsonElement(object? source)
        {
            if (source == null)
            {
                return null;
            }

            if (source is JsonElement jsonElement)
            {
                return jsonElement;
            }

            try
            {
                return JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(source, JsonDefaults.Options),
                    JsonDefaults.Options);
            }
            catch
            {
                return null;
            }
        }

        private static string ReadStringProperty(object? source, string name, string fallback)
        {
            var value = source?.GetType().GetProperty(name)?.GetValue(source)?.ToString();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string? ReadString(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
                {
                    return value.ToString();
                }
            }

            return null;
        }

        private static Guid? ReadGuid(JsonElement element, params string[] names)
        {
            var raw = ReadString(element, names);
            return Guid.TryParse(raw, out var value) ? value : null;
        }

        private static int? ReadInt(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
                {
                    return intValue;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out intValue))
                {
                    return intValue;
                }
            }

            return null;
        }

        private static bool? ReadBool(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value))
                {
                    continue;
                }

                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    return value.GetBoolean();
                }

                if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var boolValue))
                {
                    return boolValue;
                }
            }

            return null;
        }

        private static List<Guid> ReadGuidList(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var result = new List<Guid>();
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var propertyName in names.Skip(2))
                        {
                            if (item.TryGetProperty(propertyName, out var propertyValue) && Guid.TryParse(propertyValue.ToString(), out var objectGuid))
                            {
                                result.Add(objectGuid);
                                break;
                            }
                        }

                        continue;
                    }

                    if (Guid.TryParse(item.ToString(), out var guid))
                    {
                        result.Add(guid);
                    }
                }

                return result.Distinct().ToList();
            }

            return new List<Guid>();
        }

        private static IQueryable<TEntity> BuildGuidPredicateQuery<TEntity>(
            IQueryable<TEntity> source,
            System.Linq.Expressions.Expression<Func<TEntity, Guid>> selector,
            IReadOnlyCollection<Guid> ids)
        {
            if (ids.Count == 0)
            {
                return source.Where(_ => false);
            }

            var parameter = selector.Parameters[0];
            System.Linq.Expressions.Expression? body = null;
            foreach (var id in ids)
            {
                var equals = System.Linq.Expressions.Expression.Equal(selector.Body, System.Linq.Expressions.Expression.Constant(id));
                body = body == null ? equals : System.Linq.Expressions.Expression.OrElse(body, equals);
            }

            var predicate = System.Linq.Expressions.Expression.Lambda<Func<TEntity, bool>>(body!, parameter);
            return source.Where(predicate);
        }

        private static string GetTargetType(string actionCode) => actionCode switch
        {
            ReviewActions.ApplyAdmin => "system-user",
            ReviewActions.TreeApplyRole => "tree-user",
            ReviewActions.TreeCreate or ReviewActions.TreeUpdate or ReviewActions.TreeDelete => "tree",
            ReviewActions.PoemCreate or ReviewActions.PoemUpdate or ReviewActions.PoemDelete => "poem",
            ReviewActions.MemberCreate or ReviewActions.MemberUpdate or ReviewActions.MemberDelete => "member",
            ReviewActions.MemberIdentify => "member-identify",
            ReviewActions.UnionCreate or ReviewActions.UnionDelete => "union",
            ReviewActions.UnionMemberAdd or ReviewActions.UnionMemberDelete => "union-member",
            ReviewActions.EventCreate or ReviewActions.EventUpdate or ReviewActions.EventDelete => "event",
            _ => "unknown"
        };
    }
}
