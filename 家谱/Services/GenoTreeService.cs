using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;

namespace 家谱.Services
{
    public interface IGenoTreeService
    {
        Task<GenoTree> CreateAsync(GenoTreeDtos dto, Guid ownerId, Guid operatorUserId, Guid? taskId = null);

        Task<List<GenoTree>> GetAll();

        Task<GenoTree?> GetByIdAsync(Guid id);

        Task<bool> DeleteAsync(Guid id, Guid operatorUserId, Guid? taskId = null);

        Task<List<GenoTree>> GetAccessibleTreesAsync(Guid userId);

        Task<bool> UpdateAsync(GenoTreeDtos dto, Guid guid, Guid operatorUserId, Guid? taskId = null);
    }

    public class GenoTreeService : IGenoTreeService
    {
        private readonly GenealogyDbContext _db;
        private readonly IAuditLogService _auditLogService;
        private readonly ITreePermissionService _treePermissionService;

        public GenoTreeService(
            GenealogyDbContext dbContext,
            IAuditLogService auditLogService,
            ITreePermissionService treePermissionService)
        {
            _db = dbContext;
            _auditLogService = auditLogService;
            _treePermissionService = treePermissionService;
        }

        public async Task<GenoTree> CreateAsync(GenoTreeDtos dto, Guid ownerId, Guid operatorUserId, Guid? taskId = null)
        {
            var tree = new GenoTree
            {
                TreeID = Guid.NewGuid(),
                TreeName = dto.TreeName,
                AncestorName = dto.AncestorName,
                Region = dto.Region,
                Description = dto.Description,
                IsPublic = dto.IsPublic,
                OwnerID = ownerId,
                CreateTime = DateTime.UtcNow,
                IsDel = false
            };

            await _db.GenoTrees.AddAsync(tree);
            await _db.SaveChangesAsync();
            await _treePermissionService.UpsertPermissionAsync(tree.TreeID, ownerId, 1, operatorUserId);
            await _auditLogService.WriteAsync("Geno_Trees", tree.TreeID, "CREATE", operatorUserId, new { }, new
            {
                tree.TreeID,
                tree.TreeName,
                tree.AncestorName,
                tree.Region,
                tree.Description,
                tree.OwnerID,
                tree.IsPublic,
                tree.IsDel
            }, taskId);
            return tree;
        }

        public async Task<bool> DeleteAsync(Guid id, Guid operatorUserId, Guid? taskId = null)
        {
            var tree = await _db.GenoTrees.FirstOrDefaultAsync(t => t.TreeID == id && !t.IsDel);
            if (tree == null)
            {
                return false;
            }

            var before = new
            {
                tree.TreeID,
                tree.TreeName,
                tree.AncestorName,
                tree.Region,
                tree.Description,
                tree.OwnerID,
                tree.IsPublic
            };

            tree.IsDel = true;

            var poems = await _db.GenoGenerationPoems.Where(p => p.TreeID == id && !p.IsDel).ToListAsync();
            foreach (var poem in poems)
            {
                poem.IsDel = true;
            }

            var permissions = await _db.TreePermissions.Where(p => p.TreeID == id && p.IsActive).ToListAsync();
            foreach (var permission in permissions)
            {
                permission.IsActive = false;
                permission.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync("Geno_Trees", tree.TreeID, "DELETE", operatorUserId, before, new
            {
                tree.TreeID,
                tree.TreeName,
                tree.AncestorName,
                tree.Region,
                tree.Description,
                tree.OwnerID,
                tree.IsPublic,
                tree.IsDel
            }, taskId);
            return true;
        }

        public async Task<List<GenoTree>> GetAll()
        {
            var trees = await _db.GenoTrees
                .Where(t => !t.IsDel)
                .OrderByDescending(t => t.CreateTime)
                .ToListAsync();

            await AttachPoemsAsync(trees);
            return trees;
        }

        public async Task<GenoTree?> GetByIdAsync(Guid id)
        {
            var tree = await _db.GenoTrees.FirstOrDefaultAsync(t => t.TreeID == id && !t.IsDel);
            if (tree == null)
            {
                return null;
            }

            await AttachPoemsAsync(new List<GenoTree> { tree });
            return tree;
        }

        public Task<List<GenoTree>> GetAccessibleTreesAsync(Guid userId)
        {
            return _treePermissionService.GetAccessibleTreesAsync(userId);
        }

        public async Task<bool> UpdateAsync(GenoTreeDtos dto, Guid guid, Guid operatorUserId, Guid? taskId = null)
        {
            var oldTree = await _db.GenoTrees.FirstOrDefaultAsync(t => t.TreeID == guid && !t.IsDel);
            if (oldTree == null)
            {
                return false;
            }

            var before = new
            {
                oldTree.TreeID,
                oldTree.TreeName,
                oldTree.AncestorName,
                oldTree.Region,
                oldTree.Description,
                oldTree.IsPublic
            };

            oldTree.TreeName = dto.TreeName;
            oldTree.AncestorName = dto.AncestorName;
            oldTree.Region = dto.Region;
            oldTree.Description = dto.Description;
            oldTree.IsPublic = dto.IsPublic;

            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync("Geno_Trees", oldTree.TreeID, "UPDATE", operatorUserId, before, new
            {
                oldTree.TreeID,
                oldTree.TreeName,
                oldTree.AncestorName,
                oldTree.Region,
                oldTree.Description,
                oldTree.IsPublic
            }, taskId);
            return true;
        }

        private async Task AttachPoemsAsync(List<GenoTree> trees)
        {
            if (trees.Count == 0)
            {
                return;
            }

            foreach (var tree in trees)
            {
                tree.Poems = await _db.GenoGenerationPoems
                    .Where(p => !p.IsDel && p.TreeID == tree.TreeID)
                    .OrderBy(p => p.GenerationNum)
                    .ToListAsync();
            }
        }
    }
}
