using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;
using 家谱.Models.Enums;

namespace 家谱.Services
{
    public interface ITreePermissionService
    {
        Task<TreeAccessDto> GetTreeAccessAsync(Guid treeId, Guid userId);

        Task<bool> CanViewTreeAsync(GenoTree tree, Guid userId);

        Task<bool> CanEditTreeAsync(Guid treeId, Guid userId);

        Task<bool> CanReviewTaskAsync(ReviewTask task, Guid userId);

        Task<List<TreePermissionDto>> GetPermissionsAsync(Guid treeId);

        Task<GenoTreePermission> UpsertPermissionAsync(Guid treeId, Guid userId, byte roleType, Guid? grantedBy);

        Task<List<GenoTree>> GetAccessibleTreesAsync(Guid userId);
    }

    public class TreePermissionService : ITreePermissionService
    {
        private readonly GenealogyDbContext _db;

        public TreePermissionService(GenealogyDbContext db)
        {
            _db = db;
        }

        public async Task<TreeAccessDto> GetTreeAccessAsync(Guid treeId, Guid userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == userId && u.UserStatus == 1)
                ?? throw new Exception("用户不存在");
            var tree = await _db.GenoTrees.AsNoTracking().FirstOrDefaultAsync(t => t.TreeID == treeId && !t.IsDel)
                ?? throw new Exception("家谱树不存在");

            var isSuperAdmin = user.RoleType == (byte)RoleType.SuperAdmin;
            var isOwner = tree.OwnerID == userId;

            byte? roleType = null;
            if (isOwner)
            {
                roleType = (byte)TreeRoleType.Admin;
            }
            else
            {
                roleType = await _db.TreePermissions
                    .Where(p => p.TreeID == treeId && p.UserID == userId && p.IsActive)
                    .Select(p => (byte?)p.RoleType)
                    .FirstOrDefaultAsync();
            }

            var canEdit = isSuperAdmin || roleType is (byte)TreeRoleType.Admin or (byte)TreeRoleType.Editor;
            var canManagePermissions = isSuperAdmin || roleType == (byte)TreeRoleType.Admin;

            return new TreeAccessDto
            {
                IsSuperAdmin = isSuperAdmin,
                IsOwner = isOwner,
                RoleType = roleType,
                RoleName = GetTreeRoleName(roleType, isOwner, isSuperAdmin),
                CanEdit = canEdit,
                CanReview = canEdit,
                CanManagePermissions = canManagePermissions
            };
        }

        public async Task<bool> CanViewTreeAsync(GenoTree tree, Guid userId)
        {
            if (tree.IsPublic)
            {
                return true;
            }

            var access = await GetTreeAccessAsync(tree.TreeID, userId);
            return access.IsSuperAdmin || access.IsOwner || access.RoleType.HasValue;
        }

        public async Task<bool> CanEditTreeAsync(Guid treeId, Guid userId)
        {
            var access = await GetTreeAccessAsync(treeId, userId);
            return access.CanEdit;
        }

        public async Task<bool> CanReviewTaskAsync(ReviewTask task, Guid userId)
        {
            var reviewer = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == userId && u.UserStatus == 1);
            if (reviewer == null)
            {
                return false;
            }

            if (reviewer.RoleType == (byte)RoleType.SuperAdmin)
            {
                return true;
            }

            if (task.ActionCode == ReviewActions.ApplyAdmin || task.ActionCode == ReviewActions.TreeCreate)
            {
                return false;
            }

            if (task.TreeID == null)
            {
                return false;
            }

            var access = await GetTreeAccessAsync(task.TreeID.Value, userId);
            if (task.ActionCode == ReviewActions.TreeApplyRole)
            {
                return access.CanManagePermissions;
            }

            return access.CanReview;
        }

        public async Task<List<TreePermissionDto>> GetPermissionsAsync(Guid treeId)
        {
            var tree = await _db.GenoTrees.AsNoTracking().FirstOrDefaultAsync(t => t.TreeID == treeId && !t.IsDel)
                ?? throw new Exception("家谱树不存在");

            var explicitPermissions = await _db.TreePermissions
                .AsNoTracking()
                .Where(p => p.TreeID == treeId && p.IsActive)
                .Include(p => p.User)
                .Select(p => new TreePermissionDto
                {
                    PermissionId = p.PermissionID,
                    UserId = p.UserID,
                    Username = p.User.Username,
                    RoleType = p.RoleType,
                    RoleName = ReviewActions.GetRoleDisplayName(p.RoleType),
                    Email = p.User.Email,
                    Phone = p.User.Phone,
                    GrantedAt = p.GrantedAt,
                    IsOwner = false
                })
                .ToListAsync();

            var owner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == tree.OwnerID);
            if (owner != null && explicitPermissions.All(p => p.UserId != owner.UserID))
            {
                explicitPermissions.Insert(0, new TreePermissionDto
                {
                    PermissionId = Guid.Empty,
                    UserId = owner.UserID,
                    Username = owner.Username,
                    RoleType = (byte)TreeRoleType.Admin,
                    RoleName = "树管理员",
                    Email = owner.Email,
                    Phone = owner.Phone,
                    GrantedAt = tree.CreateTime,
                    IsOwner = true
                });
            }

            return explicitPermissions;
        }

        public async Task<GenoTreePermission> UpsertPermissionAsync(Guid treeId, Guid userId, byte roleType, Guid? grantedBy)
        {
            var existing = await _db.TreePermissions
                .FirstOrDefaultAsync(p => p.TreeID == treeId && p.UserID == userId);

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
                .Where(t => !t.IsDel && t.OwnerID == userId)
                .ToListAsync();

            var treeIds = await _db.TreePermissions
                .AsNoTracking()
                .Where(p => p.UserID == userId && p.IsActive)
                .Select(p => p.TreeID)
                .ToListAsync();

            var permissionTrees = new List<GenoTree>();
            foreach (var treeId in treeIds.Distinct())
            {
                var tree = await _db.GenoTrees.AsNoTracking().FirstOrDefaultAsync(t => t.TreeID == treeId && !t.IsDel);
                if (tree != null && ownerTrees.All(t => t.TreeID != tree.TreeID))
                {
                    permissionTrees.Add(tree);
                }
            }

            var trees = ownerTrees
                .Concat(permissionTrees)
                .OrderByDescending(t => t.CreateTime)
                .ToList();

            foreach (var tree in trees)
            {
                tree.Poems = await _db.GenoGenerationPoems
                    .Where(p => !p.IsDel && p.TreeID == tree.TreeID)
                    .OrderBy(p => p.GenerationNum)
                    .ToListAsync();
            }

            return trees;
        }

        private static string GetTreeRoleName(byte? roleType, bool isOwner, bool isSuperAdmin)
        {
            if (isSuperAdmin)
            {
                return "超级管理员";
            }

            if (isOwner)
            {
                return "树管理员";
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
