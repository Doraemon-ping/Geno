using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 家谱.Common;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;
using 家谱.Models.Enums;
using 家谱.Services.Common;

namespace 家谱.Services
{
    public interface IReviewService
    {
        Task<Guid> SubmitAsync(SubmitReviewRequest dto, Guid submitterId);

        Task ApproveAsync(Guid taskId, Guid reviewerId, string notes, int action);

        Task<List<TaskDtos>> GetTaskList(Guid userId);

        Task<List<TaskDtos>> GetAll(Guid userId);
    }

    public class ReviewService : IReviewService
    {
        private readonly GenealogyDbContext _db;
        private readonly IHandleTasks _handler;
        private readonly ITreePermissionService _treePermissionService;

        public ReviewService(GenealogyDbContext db, IHandleTasks handleTasks, ITreePermissionService treePermissionService)
        {
            _db = db;
            _handler = handleTasks;
            _treePermissionService = treePermissionService;
        }

        public async Task<Guid> SubmitAsync(SubmitReviewRequest dto, Guid submitterId)
        {
            IQueryable<ReviewTask> duplicateQuery = _db.ReviewTasks.Where(t =>
                t.SubmitterID == submitterId &&
                t.ActionCode == dto.ActionCode &&
                t.Status == (byte)ReviewStatus.Pending);

            if (dto.TargetId.HasValue)
            {
                duplicateQuery = duplicateQuery.Where(t => t.TargetID == dto.TargetId);
            }

            if (dto.TreeId.HasValue)
            {
                duplicateQuery = duplicateQuery.Where(t => t.TreeID == dto.TreeId);
            }

            if (!dto.ForceCreateTask && await duplicateQuery.AnyAsync())
            {
                throw new Exception("您已提交过同类待审核申请，请等待审核结果");
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
            return task.TaskID;
        }

        public async Task ApproveAsync(Guid taskId, Guid reviewerId, string notes, int action)
        {
            var task = await _db.ReviewTasks.FirstOrDefaultAsync(t => t.TaskID == taskId);
            if (task == null || task.Status != (byte)ReviewStatus.Pending)
            {
                throw new Exception("任务不存在或已处理");
            }

            if (!await _treePermissionService.CanReviewTaskAsync(task, reviewerId))
            {
                throw new Exception("无权限处理该审核任务");
            }

            if (action != 1 && action != 2)
            {
                throw new Exception("无效的审核动作");
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                if (action == 1)
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
                        default:
                            throw new Exception($"未定义的业务操作: {task.ActionCode}");
                    }

                    task.Status = (byte)ReviewStatus.Approved;
                }
                else
                {
                    task.Status = (byte)ReviewStatus.Rejected;
                }

                task.ReviewerID = reviewerId;
                task.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
                task.ProcessedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<TaskDtos>> GetTaskList(Guid userId)
        {
            var userExists = await _db.Users.AnyAsync(u => u.UserID == userId && u.UserStatus == 1);
            if (!userExists)
            {
                throw new Exception("用户不存在");
            }

            var tasks = await _db.ReviewTasks
                .AsNoTracking()
                .Where(t => t.SubmitterID == userId || t.ReviewerID == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var result = new List<TaskDtos>(tasks.Count);
            foreach (var task in tasks)
            {
                result.Add(await MapTaskDtoAsync(task, userId));
            }

            return result;
        }

        public async Task<List<TaskDtos>> GetAll(Guid userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == userId && u.UserStatus == 1);
            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            var tasks = await _db.ReviewTasks
                .AsNoTracking()
                .Where(t => t.Status == (byte)ReviewStatus.Pending)
                .OrderByDescending(t => t.CreatedAt)
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

        private async Task<TaskDtos> MapTaskDtoAsync(ReviewTask task, Guid currentUserId, bool? canProcessOverride = null)
        {
            var changeData = DeserializeJson(task.ChangeData);
            var submitter = await BuildUserSummaryAsync(task.SubmitterID);
            var reviewer = task.ReviewerID.HasValue ? await BuildUserSummaryAsync(task.ReviewerID.Value) : null;
            var treeSummary = task.TreeID.HasValue ? await BuildTreeSummaryAsync(task.TreeID.Value) : BuildTreeSummaryFromPayload(task.ActionCode, changeData);
            var targetSummary = await BuildTargetSummaryAsync(task, changeData);

            var canProcess = canProcessOverride ?? await _treePermissionService.CanReviewTaskAsync(task, currentUserId);

            return new TaskDtos
            {
                ActionCode = task.ActionCode,
                TaskId = task.TaskID,
                SubmitterName = ReadStringProperty(submitter, "username", "未知"),
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
                CanProcess = canProcess
            };
        }

        private async Task<object?> BuildTargetSummaryAsync(ReviewTask task, object? changeData)
        {
            return task.ActionCode switch
            {
                ReviewActions.ApplyAdmin or ReviewActions.TreeApplyRole => await BuildUserSummaryAsync(task.TargetID),
                ReviewActions.TreeCreate => BuildTreeSummaryFromPayload(task.ActionCode, changeData),
                ReviewActions.TreeUpdate or ReviewActions.TreeDelete => await BuildTreeSummaryAsync(task.TargetID ?? task.TreeID),
                ReviewActions.PoemCreate => BuildPoemSummaryFromPayload(changeData),
                ReviewActions.PoemUpdate or ReviewActions.PoemDelete => await BuildPoemSummaryAsync(task.TargetID),
                _ => null
            };
        }

        private async Task<object?> BuildUserSummaryAsync(Guid? userId)
        {
            if (userId == null)
            {
                return null;
            }

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == userId.Value);
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

            var tree = await _db.GenoTrees.AsNoTracking().FirstOrDefaultAsync(t => t.TreeID == treeId.Value);
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
            if (payload == null)
            {
                return null;
            }

            if (actionCode is not (ReviewActions.TreeCreate or ReviewActions.TreeUpdate))
            {
                return null;
            }

            var tree = JsonSerializer.Deserialize<GenoTreeDtos>(JsonSerializer.Serialize(payload, JsonDefaults.Options), JsonDefaults.Options);
            if (tree == null)
            {
                return null;
            }

            return new
            {
                treeName = tree.TreeName,
                ancestorName = tree.AncestorName,
                region = tree.Region,
                description = tree.Description,
                isPublic = tree.IsPublic
            };
        }

        private async Task<object?> BuildPoemSummaryAsync(Guid? poemId)
        {
            if (poemId == null)
            {
                return null;
            }

            var poem = await _db.GenoGenerationPoems.AsNoTracking().FirstOrDefaultAsync(p => p.PoemID == poemId.Value);
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
            if (payload == null)
            {
                return null;
            }

            var poem = JsonSerializer.Deserialize<PoemDto>(JsonSerializer.Serialize(payload, JsonDefaults.Options), JsonDefaults.Options);
            if (poem == null)
            {
                return null;
            }

            return new
            {
                treeId = poem.TreeId,
                generationNum = poem.GenerationNum,
                word = poem.Word,
                meaning = poem.Meaning
            };
        }

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

        private static string ReadStringProperty(object? source, string name, string fallback)
        {
            var value = source?.GetType().GetProperty(name)?.GetValue(source)?.ToString();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string GetTargetType(string actionCode) => actionCode switch
        {
            ReviewActions.ApplyAdmin => "system-user",
            ReviewActions.TreeApplyRole => "tree-user",
            ReviewActions.TreeCreate or ReviewActions.TreeUpdate or ReviewActions.TreeDelete => "tree",
            ReviewActions.PoemCreate or ReviewActions.PoemUpdate or ReviewActions.PoemDelete => "poem",
            _ => "unknown"
        };
    }
}
