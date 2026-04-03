namespace 家谱.Services
{
    using Microsoft.EntityFrameworkCore;
    using 家谱.DB;
    using 家谱.Models.DTOs;
    using 家谱.Models.Entities;

    // 这个服务类目前是空的，未来可以添加与家谱树相关的业务逻辑方法，例如：

    public interface IGenoTreeService
    {
        // 创建家谱树

        Task<bool> CreateAsync(GenoTreeDtos dto);

        // 获取所有家谱树列表

        Task<List<GenoTree>> GetAll();

        // 根据 ID 获取家谱树详情

        Task<GenoTree?> GetByIdAsync(Guid id);

        //deletr

        Task<bool> DeleteAsync(Guid id);

        //GetByOwnerIdAsync(Guid ownerId);

        Task<List<GenoTree>> GetByOwner(Guid owner);

        //UpdateAsync(GenoTreeDtos dto);

        Task<bool> UpdateAsync(GenoTreeDtos dto, Guid guid);
    }

    public class GenoTreeService : IGenoTreeService
    {
        private readonly GenealogyDbContext _db;

        public GenoTreeService(GenealogyDbContext dbContext)
        {
            _db = dbContext;
        }

        // 创建家谱树

        public async Task<bool> CreateAsync(GenoTreeDtos dto)
        {

            GenoTree tree = new GenoTree
            {
                TreeName = dto.TreeName,
                AncestorName = dto.AncestorName,
                Region = dto.Region,
                Description = dto.Description,
                IsPublic = dto.IsPublic,
                OwnerID = dto.Owner
            };
            await _db.GenoTrees.AddAsync(tree);
            return await _db.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var tree = _db.GenoTrees.Find(id);
            if (tree != null)
            {
                _db.GenoTrees.Remove(tree);
                return await _db.SaveChangesAsync() > 0;
            }
            return false;
        }

        // 获取所有家谱树列表

        public async Task<List<GenoTree>> GetAll()
        {
            var trees = await _db.GenoTrees
                .Include(t => t.Poems) // 包含字辈信息
                .ToListAsync();
            return trees;
        }

        // 根据 ID 获取家谱树详情

        public async Task<GenoTree?> GetByIdAsync(Guid id)
        {
            return await _db.GenoTrees
                .Include(t => t.Poems) // 包含字辈信息
                .FirstOrDefaultAsync(t => t.TreeID == id);
        }

        // 根据所有者 ID 获取家谱树列表

        public async Task<List<GenoTree>> GetByOwner(Guid owner)
        {
            return await _db.GenoTrees
                .Include(t => t.Poems)
                .Where(t => t.OwnerID == owner).ToListAsync();
        }

        public async Task<bool> UpdateAsync(GenoTreeDtos dto, Guid guid)
        {
            var oldTree = await _db.GenoTrees.FindAsync(guid);
            if (oldTree == null) return false;
            oldTree.TreeName = dto.TreeName;
            oldTree.AncestorName = dto.AncestorName;
            oldTree.Region = dto.Region;
            oldTree.Description = dto.Description;
            oldTree.IsPublic = dto.IsPublic;
            return await _db.SaveChangesAsync() > 0;
        }
    }
}
