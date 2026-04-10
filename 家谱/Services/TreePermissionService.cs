using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;
using 家谱.Models.Enums;

namespace 家谱.Services
{
    /// <summary>
    /// 树权限服务接口。
    /// </summary>
    public interface ITreePermissionService
    {
        /// <summary>
        /// 获取指定用户在当前树上的权限能力。公开树支持匿名访问。
        /// </summary>
        Task<TreeAccessDto> GetTreeAccessAsync(Guid treeId, Guid? userId);

        /// <summary>
        /// 判断用户是否可以查看树。
        /// </summary>
        Task<bool> CanViewTreeAsync(GenoTree tree, Guid? userId);

        /// <summary>
        /// 判断用户是否可以直接修改树内容。
        /// </summary>
        Task<bool> CanDirectEditTreeAsync(Guid treeId, Guid userId);

        /// <summary>
        /// 判断用户是否可以提交树内修改申请。
        /// </summary>
        Task<bool> CanSubmitTreeChangeAsync(Guid treeId, Guid userId);

        /// <summary>
        /// 判断用户是否可以审核任务。
        /// </summary>
        Task<bool> CanReviewTaskAsync(ReviewTask task, Guid userId);

        /// <summary>
        /// 获取树内已授权成员列表。
        /// </summary>
        Task<List<TreePermissionDto>> GetPermissionsAsync(Guid treeId);

        /// <summary>
        /// 新增或更新树权限。
        /// </summary>
        Task<GenoTreePermission> UpsertPermissionAsync(Guid treeId, Guid userId, byte roleType, Guid? grantedBy);

        /// <summary>
        /// 获取用户可访问的树列表。
        /// </summary>
        Task<List<GenoTree>> GetAccessibleTreesAsync(Guid userId);
    }

    /// <summary>
    /// 树权限服务实现。
    /// </summary>
    public class TreePermissionService : ITreePermissionService
    {
        private readonly GenealogyDbContext _db;

        public TreePermissionService(GenealogyDbContext db)
        {
            _db = db;
        }

        public async Task<TreeAccessDto> GetTreeAccessAsync(Guid treeId, Guid? userId)
        {
            var tree = await _db.GenoTrees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TreeID == treeId && !item.IsDel)
                ?? throw new Exception("家谱树不存在");

            if (!userId.HasValue)
            {
                return BuildVisitorAccess(tree, false);
            }

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserID == userId.Value && item.UserStatus == 1)
                ?? throw new Exception("用户不存在");

            var isSuperAdmin = user.RoleType == (byte)RoleType.SuperAdmin;
            var isSystemAdmin = user.RoleType == (byte)RoleType.Admin;
            var isOwner = tree.OwnerID == user.UserID;

            byte? roleType = null;
            if (!isOwner)
            {
                roleType = await _db.TreePermissions
                    .AsNoTracking()
                    .Where(permission =>
                        permission.TreeID == treeId &&
                        permission.UserID == user.UserID &&
                        permission.IsActive)
                    .Select(permission => (byte?)permission.RoleType)
                    .FirstOrDefaultAsync();
            }

            var canView = isSuperAdmin || tree.IsPublic || isOwner || roleType.HasValue;
            var canDirectEdit = isSuperAdmin || isOwner || roleType == (byte)TreeRoleType.Admin;
            var canSubmitChange = canDirectEdit || roleType == (byte)TreeRoleType.Editor;
            var canReview = isSuperAdmin || isOwner || roleType == (byte)TreeRoleType.Admin;
            var canManagePermissions = isSuperAdmin || isOwner || roleType == (byte)TreeRoleType.Admin;

            return new TreeAccessDto
            {
                IsSuperAdmin = isSuperAdmin,
                IsSystemAdmin = isSystemAdmin,
                IsOwner = isOwner,
                RoleType = isOwner ? (byte)TreeRoleType.Admin : roleType,
                RoleName = GetTreeRoleName(isSuperAdmin, isOwner, roleType),
                CanView = canView,
                CanSubmitChange = canSubmitChange,
                CanDirectEdit = canDirectEdit,
                CanEdit = canDirectEdit,
                CanReview = canReview,
                CanManagePermissions = canManagePermissions
            };
        }

        public async Task<bool> CanViewTreeAsync(GenoTree tree, Guid? userId)
        {
            if (tree.IsPublic)
            {
                return true;
            }

            if (!userId.HasValue)
            {
                return false;
            }

            var access = await GetTreeAccessAsync(tree.TreeID, userId.Value);
            return access.CanView;
        }

        public async Task<bool> CanDirectEditTreeAsync(Guid treeId, Guid userId)
        {
            var access = await GetTreeAccessAsync(treeId, userId);
            return access.CanDirectEdit;
        }

        public async Task<bool> CanSubmitTreeChangeAsync(Guid treeId, Guid userId)
        {
            var access = await GetTreeAccessAsync(treeId, userId);
            return access.CanSubmitChange;
        }

        public async Task<bool> CanReviewTaskAsync(ReviewTask task, Guid userId)
        {
            var reviewer = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.UserID == userId && user.UserStatus == 1);

            if (reviewer == null)
            {
                return false;
            }

            if (task.ActionCode == ReviewActions.ApplyAdmin)
            {
                return reviewer.RoleType == (byte)RoleType.SuperAdmin;
            }

            if (task.ActionCode == ReviewActions.TreeCreate)
            {
                return reviewer.RoleType is (byte)RoleType.SuperAdmin or (byte)RoleType.Admin;
            }

            if (!task.TreeID.HasValue)
            {
                return false;
            }

            var access = await GetTreeAccessAsync(task.TreeID.Value, userId);
            if (!access.CanView)
            {
                return false;
            }

            if (task.ActionCode == ReviewActions.TreeApplyRole)
            {
                return access.CanManagePermissions;
            }

            return access.CanReview;
        }

        public async Task<List<TreePermissionDto>> GetPermissionsAsync(Guid treeId)
        {
            var tree = await _db.GenoTrees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TreeID == treeId && !item.IsDel)
                ?? throw new Exception("家谱树不存在");

            var permissions = await _db.TreePermissions
                .AsNoTracking()
                .Where(permission => permission.TreeID == treeId && permission.IsActive)
                .Include(permission => permission.User)
                .Select(permission => new TreePermissionDto
                {
                    PermissionId = permission.PermissionID,
                    UserId = permission.UserID,
                    Username = permission.User.Username,
                    RoleType = permission.RoleType,
                    RoleName = ReviewActions.GetTreeRoleDisplayName(permission.RoleType),
                    Email = permission.User.Email,
                    Phone = permission.User.Phone,
                    GrantedAt = permission.GrantedAt,
                    IsOwner = false
                })
                .ToListAsync();

            var owner = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.UserID == tree.OwnerID && user.UserStatus == 1);

            if (owner != null && permissions.All(permission => permission.UserId != owner.UserID))
            {
                permissions.Insert(0, new TreePermissionDto
                {
                    PermissionId = Guid.Empty,
                    UserId = owner.UserID,
                    Username = owner.Username,
                    RoleType = (byte)TreeRoleType.Admin,
                    RoleName = "树拥有者",
                    Email = owner.Email,
                    Phone = owner.Phone,
                    GrantedAt = tree.CreateTime,
                    IsOwner = true
                });
            }

            return permissions;
        }

        public async Task<GenoTreePermission> UpsertPermissionAsync(Guid treeId, Guid userId, byte roleType, Guid? grantedBy)
        {
            var existing = await _db.TreePermissions
                .FirstOrDefaultAsync(permission => permission.TreeID == treeId && permission.UserID == userId);

            if (existing == null)
            {
                existing = new GenoTreePermission
                {
                    PermissionID = Guid.NewGuid(),
                    TreeID = treeId,
                    UserID = userId,
                    RoleType = roleType,
                    GrantedBy = grantedBy,
                    GrantedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _db.TreePermissions.Add(existing);
            }
            else
            {
                existing.RoleType = roleType;
                existing.GrantedBy = grantedBy;
                existing.GrantedAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsActive = true;
            }

            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task<List<GenoTree>> GetAccessibleTreesAsync(Guid userId)
        {
            var ownerTrees = await _db.GenoTrees
                .AsNoTracking()
                .Where(tree => !tree.IsDel && tree.OwnerID == userId)
                .OrderByDescending(tree => tree.CreateTime)
                .ToListAsync();

            var treeIds = await _db.TreePermissions
                .AsNoTracking()
                .Where(permission => permission.UserID == userId && permission.IsActive)
                .Select(permission => permission.TreeID)
                .Distinct()
                .ToListAsync();

            var permissionTrees = new List<GenoTree>();
            foreach (var treeId in treeIds)
            {
                var tree = await _db.GenoTrees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.TreeID == treeId && !item.IsDel);

                if (tree != null && ownerTrees.All(item => item.TreeID != tree.TreeID))
                {
                    permissionTrees.Add(tree);
                }
            }

            var trees = ownerTrees
                .Concat(permissionTrees)
                .OrderByDescending(tree => tree.CreateTime)
                .ToList();

            foreach (var tree in trees)
            {
                tree.Poems = await _db.GenoGenerationPoems
                    .AsNoTracking()
                    .Where(poem => poem.TreeID == tree.TreeID && !poem.IsDel)
                    .OrderBy(poem => poem.GenerationNum)
                    .ToListAsync();
            }

            return trees;
        }

        private static TreeAccessDto BuildVisitorAccess(GenoTree tree, bool isSystemAdmin)
        {
            var canView = tree.IsPublic;
            return new TreeAccessDto
            {
                IsSuperAdmin = false,
                IsSystemAdmin = isSystemAdmin,
                IsOwner = false,
                RoleType = null,
                RoleName = "访客",
                CanView = canView,
                CanSubmitChange = false,
                CanDirectEdit = false,
                CanEdit = false,
                CanReview = false,
                CanManagePermissions = false
            };
        }

        private static string GetTreeRoleName(bool isSuperAdmin, bool isOwner, byte? roleType)
        {
            if (isSuperAdmin)
            {
                return "超级管理员";
            }

            if (isOwner)
            {
                return "树拥有者";
            }

            return roleType switch
            {
                (byte)TreeRoleType.Admin => "树管理员",
                (byte)TreeRoleType.Editor => "修谱员",
                _ => "访客"
            };
        }
    }
}
