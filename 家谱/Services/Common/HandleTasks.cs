using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 家谱.Common;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Entities;
using 家谱.Models.Enums;

namespace 家谱.Services.Common
{
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
    }

    public class HandleTasks : IHandleTasks
    {
        private readonly GenealogyDbContext _db;
        private readonly IGenoTreeService _treeService;
        private readonly IGenoPoemService _poemService;
        private readonly ITreePermissionService _treePermissionService;
        private readonly IAuditLogService _auditLogService;

        public HandleTasks(
            GenealogyDbContext db,
            IGenoTreeService treeService,
            IGenoPoemService poemService,
            ITreePermissionService treePermissionService,
            IAuditLogService auditLogService)
        {
            _db = db;
            _treeService = treeService;
            _poemService = poemService;
            _treePermissionService = treePermissionService;
            _auditLogService = auditLogService;
        }

        public async Task HandleApplyAdminAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<RoleApplyPayload>(task.ChangeData, JsonDefaults.Options)
                ?? throw new Exception("无效的权限申请数据");
            var user = await _db.Users.FirstOrDefaultAsync(r => r.UserID == task.TargetID)
                ?? throw new Exception("用户不存在");

            if (user.RoleType == payload.NewRole)
            {
                throw new Exception($"用户已是{ReviewActions.GetRoleDisplayName(payload.NewRole)}，无需重复申请");
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
            await _auditLogService.WriteAsync("Sys_Users", user.UserID, "UPDATE", reviewerId, before, task.TaskID);
        }

        public async Task HandleTreeApplyRoleAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<TreePermissionApplyDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new Exception("无效的树权限申请数据");

            var targetUserId = task.TargetID ?? payload.TargetUserId ?? throw new Exception("缺少目标用户");
            var permission = await _treePermissionService.UpsertPermissionAsync(payload.TreeId, targetUserId, payload.NewRole, reviewerId);

            task.TreeID = payload.TreeId;
            task.TargetID = targetUserId;

            await _auditLogService.WriteAsync("Geno_Tree_Permissions", permission.PermissionID, "UPDATE", reviewerId, new
            {
                TreeID = payload.TreeId,
                UserID = targetUserId,
                OldRole = (byte?)null
            }, task.TaskID);
        }

        public async Task HandleTreeCreateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoTreeDtos>(task.ChangeData, JsonDefaults.Options)
                ?? throw new Exception("无效的家谱树数据");

            var tree = await _treeService.CreateAsync(payload, task.SubmitterID, reviewerId, task.TaskID);
            task.TreeID = tree.TreeID;
            task.TargetID = tree.TreeID;
        }

        public async Task HandleTreeUpdateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<GenoTreeDtos>(task.ChangeData, JsonDefaults.Options)
                ?? throw new Exception("无效的家谱树更新数据");

            if (task.TargetID == null)
            {
                throw new Exception("缺少目标树");
            }

            var success = await _treeService.UpdateAsync(payload, task.TargetID.Value, reviewerId, task.TaskID);
            if (!success)
            {
                throw new Exception("家谱树不存在");
            }
        }

        public async Task HandleTreeDeleteAsync(ReviewTask task, Guid reviewerId)
        {
            var treeId = task.TargetID ?? task.TreeID ?? throw new Exception("缺少目标树");
            var success = await _treeService.DeleteAsync(treeId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new Exception("家谱树不存在");
            }
        }

        public async Task HandlePoemCreateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<PoemDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new Exception("无效的字辈数据");

            var poem = await _poemService.CreateAsync(payload, reviewerId, task.TaskID);
            task.TreeID = payload.TreeId;
            task.TargetID = poem.PoemID;
        }

        public async Task HandlePoemUpdateAsync(ReviewTask task, Guid reviewerId)
        {
            var payload = JsonSerializer.Deserialize<PoemDto>(task.ChangeData, JsonDefaults.Options)
                ?? throw new Exception("无效的字辈更新数据");
            var poemId = task.TargetID ?? throw new Exception("缺少目标字辈");

            var success = await _poemService.UpdateAsync(payload, poemId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new Exception("字辈不存在");
            }
        }

        public async Task HandlePoemDeleteAsync(ReviewTask task, Guid reviewerId)
        {
            var poemId = task.TargetID ?? throw new Exception("缺少目标字辈");
            var success = await _poemService.DeleteAsync(poemId, reviewerId, task.TaskID);
            if (!success)
            {
                throw new Exception("字辈不存在");
            }
        }
    }
}
