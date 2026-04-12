using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 家谱.Common;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Entities;
using 家谱.Models.Enums;
using 家谱.Services;

namespace 家谱.Services.Common
{
    /// <summary>
    /// 审核通过后的业务执行器接口。
    /// </summary>
    public interface IHandleTasks
    {
        Task HandleApplyAdminAsync(ReviewTask task, Guid reviewerId);

        Task HandleTreeApplyRoleAsync(ReviewTask task, Guid reviewerId);

        Task HandleTreeCreateAsync(ReviewTask task, Guid reviewerId);

        Task HandleTreeUpdateAsync(ReviewTask task, Guid reviewerId);

        Task HandleTreeDeleteAsync(ReviewTask task, Guid reviewerId);

        Task HandlePoemCreateAsync(ReviewTask task, Guid reviewerId);

        Task HandlePoemUpdateAsync(ReviewTask task, Guid reviewerId);

        Task HandlePoemDeleteAsync(ReviewTask task, Guid reviewerId);

        Task HandleMemberCreateAsync(ReviewTask task, Guid reviewerId);

        Task HandleMemberUpdateAsync(ReviewTask task, Guid reviewerId);

        Task HandleMemberDeleteAsync(ReviewTask task, Guid reviewerId);

        Task HandleMemberIdentifyAsync(ReviewTask task, Guid reviewerId);

        Task HandleUnionCreateAsync(ReviewTask task, Guid reviewerId);

        Task HandleUnionUpdateAsync(ReviewTask task, Guid reviewerId);

        Task HandleUnionDeleteAsync(ReviewTask task, Guid reviewerId);

        Task HandleUnionMemberAddAsync(ReviewTask task, Guid reviewerId);

        Task HandleUnionMemberDeleteAsync(ReviewTask task, Guid reviewerId);

        Task HandleEventCreateAsync(ReviewTask task, Guid reviewerId);

        Task HandleEventUpdateAsync(ReviewTask task, Guid reviewerId);

        Task HandleEventDeleteAsync(ReviewTask task, Guid reviewerId);
    }

    /// <summary>
    /// 审核任务执行器。
    /// </summary>
    public class HandleTasks : IHandleTasks
    {
        private readonly GenealogyDbContext _db;
        private readonly IGenoTreeService _treeService;
        private readonly IGenoPoemService _poemService;
        private readonly IGenoMemberService _memberService;
        private readonly IGenoUnionService _unionService;
        private readonly IGenoEventService _eventService;
        private readonly ITreePermissionService _treePermissionService;
        private readonly IAuditLogService _auditLogService;

        public HandleTasks(
            GenealogyDbContext db,
            IGenoTreeService treeService,
            IGenoPoemService poemService,
            IGenoMemberService memberService,
            IGenoUnionService unionService,
            IGenoEventService eventService,
            ITreePermissionService treePermissionService,
            IAuditLogService auditLogService)
        {
            _db = db;
            _treeService = treeService;
            _poemService = poemService;
            _memberService = memberService;
            _unionService = unionService;
            _eventService = eventService;
            _treePermissionService = treePermissionService;
            _auditLogService = auditLogService;
        }

        public async Task HandleApplyAdminAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<RoleApplyPayload>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的权限申请数据");

            var user = await _db.Users.FirstOrDefaultAsync(item => item.UserID == task.TargetID)
                ?? throw new KeyNotFoundException("用户不存在");

            if (user.RoleType == payload.NewRole)
            {
                throw new InvalidOperationException($"用户已经是 {ReviewActions.GetRoleDisplayName(payload.NewRole)}，无需重复申请");
            }

            var before = new
            {
                user.UserID,
                user.Username,
                user.RoleType,
                user.Email,
                user.Phone
            };

            user.RoleType = payload.NewRole;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Sys_Users",
                user.UserID,
                "UPDATE",
                reviewerId,
                before,
                new
                {
                    user.UserID,
                    user.Username,
                    user.RoleType,
                    user.Email,
                    user.Phone
                },
                task.TaskID);
        }

        public async Task HandleTreeApplyRoleAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<TreePermissionApplyDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的树权限申请数据");

            var targetUserId = task.TargetID ?? payload.TargetUserId ?? throw new InvalidOperationException("缺少目标用户");
            var existing = await _db.TreePermissions
                .AsNoTracking()
                .FirstOrDefaultAsync(permission => permission.TreeID == payload.TreeId && permission.UserID == targetUserId);

            var permission = await _treePermissionService.UpsertPermissionAsync(payload.TreeId, targetUserId, payload.NewRole, reviewerId);

            task.TreeID = payload.TreeId;
            task.TargetID = targetUserId;

            await _auditLogService.WriteAsync(
                "Geno_Tree_Permissions",
                permission.PermissionID,
                "UPDATE",
                reviewerId,
                new
                {
                    TreeID = payload.TreeId,
                    UserID = targetUserId,
                    OldRole = existing?.RoleType
                },
                new
                {
                    permission.PermissionID,
                    permission.TreeID,
                    permission.UserID,
                    permission.RoleType,
                    permission.IsActive
                },
                task.TaskID);
        }

        public async Task HandleTreeCreateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoTreeDtos>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的家谱树数据");

            var tree = await _treeService.CreateAsync(payload, task.SubmitterID, reviewerId, task.TaskID);
            task.TreeID = tree.TreeID;
            task.TargetID = tree.TreeID;
        }

        public async Task HandleTreeUpdateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoTreeDtos>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的家谱树更新数据");

            var targetTreeId = task.TargetID ?? throw new InvalidOperationException("缺少目标树");
            var success = await _treeService.UpdateAsync(payload, targetTreeId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("家谱树不存在");
            }
        }

        public async Task HandleTreeDeleteAsync(ReviewTask task, Guid reviewerId)
        {
            var treeId = task.TargetID ?? task.TreeID ?? throw new InvalidOperationException("缺少目标树");
            var success = await _treeService.DeleteAsync(treeId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("家谱树不存在");
            }
        }

        public async Task HandlePoemCreateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<PoemDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的字辈数据");

            var poem = await _poemService.CreateAsync(payload, reviewerId, task.TaskID);
            task.TreeID = payload.TreeId;
            task.TargetID = poem.PoemID;
        }

        public async Task HandlePoemUpdateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<PoemDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的字辈更新数据");

            var poemId = task.TargetID ?? throw new InvalidOperationException("缺少目标字辈");
            var success = await _poemService.UpdateAsync(payload, poemId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("字辈不存在");
            }
        }

        public async Task HandlePoemDeleteAsync(ReviewTask task, Guid reviewerId)
        {
            var poemId = task.TargetID ?? throw new InvalidOperationException("缺少目标字辈");
            var success = await _poemService.DeleteAsync(poemId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("字辈不存在");
            }
        }

        public async Task HandleMemberCreateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoMemberDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的成员数据");

            var member = await _memberService.CreateAsync(payload, reviewerId, task.TaskID);
            task.TreeID = payload.TreeId;
            task.TargetID = member.MemberID;
        }

        public async Task HandleMemberUpdateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoMemberDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的成员更新数据");

            var memberId = task.TargetID ?? throw new InvalidOperationException("缺少目标成员");
            var success = await _memberService.UpdateAsync(memberId, payload, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("树成员不存在");
            }

            task.TreeID = payload.TreeId;
        }

        public async Task HandleMemberDeleteAsync(ReviewTask task, Guid reviewerId)
        {
            var memberId = task.TargetID ?? throw new InvalidOperationException("缺少目标成员");
            var success = await _memberService.DeleteAsync(memberId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("树成员不存在");
            }
        }

        public async Task HandleMemberIdentifyAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<MemberIdentifyApplyDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的成员认领申请数据");

            var targetUserId = task.SubmitterID;
            var member = await _db.GenoMembers
                .FirstOrDefaultAsync(item => item.MemberID == payload.MemberId && item.TreeID == payload.TreeId && item.IsDel != true)
                ?? throw new KeyNotFoundException("树成员不存在");

            if (member.SysUserId.HasValue && member.SysUserId.Value != targetUserId)
            {
                throw new InvalidOperationException("该树成员已绑定其他系统用户");
            }

            var alreadyBound = await _db.GenoMembers
                .AsNoTracking()
                .AnyAsync(item =>
                    item.TreeID == payload.TreeId &&
                    item.MemberID != payload.MemberId &&
                    item.SysUserId == targetUserId &&
                    item.IsDel != true);

            if (alreadyBound)
            {
                throw new InvalidOperationException("当前账号已经绑定到该家谱树的其他成员");
            }

            var before = new
            {
                member.MemberID,
                member.TreeID,
                member.LastName,
                member.FirstName,
                member.SysUserId
            };

            member.SysUserId = targetUserId;
            await _db.SaveChangesAsync();

            task.TreeID = payload.TreeId;
            task.TargetID = member.MemberID;

            await _auditLogService.WriteAsync(
                "Geno_Members",
                member.MemberID,
                "UPDATE",
                reviewerId,
                before,
                new
                {
                    member.MemberID,
                    member.TreeID,
                    member.LastName,
                    member.FirstName,
                    member.SysUserId
                },
                task.TaskID);
        }

        public async Task HandleUnionCreateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoUnionDto>(task.ChangeData, JsonDefaults.Options)
                ?? JsonSerializer.Deserialize<GenoUnionDto>(NormalizeUnionPayload(task.ChangeData), JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的婚姻单元数据");

            var union = await _unionService.CreateAsync(payload, reviewerId, task.TaskID);
            task.TreeID = payload.TreeId;
            task.TargetID = union.UnionID;
        }

        public async Task HandleUnionUpdateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoUnionDto>(task.ChangeData, JsonDefaults.Options)
                ?? JsonSerializer.Deserialize<GenoUnionDto>(NormalizeUnionPayload(task.ChangeData), JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的婚姻单元数据");

            var unionId = task.TargetID ?? throw new InvalidOperationException("缺少目标婚姻单元");
            var success = await _unionService.UpdateAsync(unionId, payload, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("婚姻单元不存在");
            }

            task.TreeID = payload.TreeId;
        }

        public async Task HandleUnionDeleteAsync(ReviewTask task, Guid reviewerId)
        {
            var unionId = task.TargetID ?? throw new InvalidOperationException("缺少目标婚姻单元");
            var success = await _unionService.DeleteAsync(unionId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("婚姻单元不存在");
            }
        }

        public async Task HandleUnionMemberAddAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoUnionMemberDto>(task.ChangeData, JsonDefaults.Options)
                ?? JsonSerializer.Deserialize<GenoUnionMemberDto>(NormalizeUnionMemberPayload(task.ChangeData), JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的家庭子女关联数据");

            var relation = await _unionService.AddMemberAsync(payload, reviewerId, task.TaskID);
            task.TreeID = payload.TreeId;
            task.TargetID = relation.MemberID;
        }

        public async Task HandleUnionMemberDeleteAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoUnionMemberDto>(task.ChangeData, JsonDefaults.Options)
                ?? JsonSerializer.Deserialize<GenoUnionMemberDto>(NormalizeUnionMemberPayload(task.ChangeData), JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的家庭子女关联删除数据");

            var success = await _unionService.RemoveMemberAsync(payload.UnionId, payload.MemberId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("家庭子女关联不存在");
            }

            task.TreeID = payload.TreeId;
            task.TargetID = payload.MemberId;
        }

        public async Task HandleEventCreateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoEventDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的历史事件数据");

            var entity = await _eventService.CreateAsync(payload, reviewerId, task.TaskID);
            task.TreeID = entity.TreeID;
            task.TargetID = entity.EventID;
        }

        public async Task HandleEventUpdateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoEventDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new InvalidOperationException("无效的历史事件更新数据");

            var eventId = task.TargetID ?? throw new InvalidOperationException("缺少目标历史事件");
            var success = await _eventService.UpdateAsync(eventId, payload, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("历史事件不存在");
            }
        }

        public async Task HandleEventDeleteAsync(ReviewTask task, Guid reviewerId)
        {
            var eventId = task.TargetID ?? throw new InvalidOperationException("缺少目标历史事件");
            var success = await _eventService.DeleteAsync(eventId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new KeyNotFoundException("历史事件不存在");
            }
        }

        private static string NormalizeUnionPayload(string changeData)
        {
            using var document = JsonDocument.Parse(changeData);
            var root = document.RootElement;
            return JsonSerializer.Serialize(new GenoUnionDto
            {
                TreeId = ReadGuid(root, "treeId", "TreeId") ?? Guid.Empty,
                Partner1Id = ReadGuid(root, "partner1Id", "Partner1Id") ?? Guid.Empty,
                Partner2Id = ReadGuid(root, "partner2Id", "Partner2Id") ?? Guid.Empty,
                UnionType = ReadByte(root, "unionType", "UnionType") ?? 1,
                SortOrder = ReadInt(root, "sortOrder", "SortOrder") ?? 1,
                MarriageDate = ReadDate(root, "marriageDate", "MarriageDate")
            }, JsonDefaults.Options);
        }

        private static string NormalizeUnionMemberPayload(string changeData)
        {
            using var document = JsonDocument.Parse(changeData);
            var root = document.RootElement;
            return JsonSerializer.Serialize(new GenoUnionMemberDto
            {
                TreeId = ReadGuid(root, "treeId", "TreeId") ?? Guid.Empty,
                UnionId = ReadGuid(root, "unionId", "UnionId") ?? Guid.Empty,
                MemberId = ReadGuid(root, "memberId", "MemberId") ?? Guid.Empty,
                RelType = ReadByte(root, "relType", "RelType") ?? 1,
                ChildOrder = ReadInt(root, "childOrder", "ChildOrder") ?? 1
            }, JsonDefaults.Options);
        }

        private static Guid? ReadGuid(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out var value) && Guid.TryParse(value.ToString(), out var guid))
                {
                    return guid;
                }
            }

            return null;
        }

        private static int? ReadInt(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                {
                    return number;
                }

                if (int.TryParse(value.ToString(), out number))
                {
                    return number;
                }
            }

            return null;
        }

        private static byte? ReadByte(JsonElement element, params string[] names)
        {
            var number = ReadInt(element, names);
            return number.HasValue ? (byte?)number.Value : null;
        }

        private static DateTime? ReadDate(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out var value) && DateTime.TryParse(value.ToString(), out var date))
                {
                    return date;
                }
            }

            return null;
        }
    }
}
