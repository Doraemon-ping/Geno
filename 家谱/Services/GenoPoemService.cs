namespace 家谱.Services
{
    using Microsoft.EntityFrameworkCore;
    using 家谱.DB;
    using 家谱.Models.DTOs;
    using 家谱.Models.Entities;

    public interface IGenoPoemService
    {
        // 查：获取某棵家谱树的所有字辈，按代数排序
        Task<List<GenoGenerationPoem>> GetByTreeIdAsync(Guid treeId);

        // 查：获取单个字辈详情
        Task<GenoGenerationPoem?> GetByIdAsync(Guid id);

        Task<bool> CreateAsync(PoemDto dto);

        Task<bool> UpdateAsync(PoemDto dto, Guid PoemId);

        Task<bool> DeleteAsync(Guid id);
    }

    public class GenoPoemService : IGenoPoemService
    {
        private readonly GenealogyDbContext _context;

        public GenoPoemService(GenealogyDbContext context)
        {
            _context = context;
        }

        // 查：获取某棵家谱树的所有字辈，按代数排序

        public async Task<List<GenoGenerationPoem>> GetByTreeIdAsync(Guid treeId)
        {
            return await _context.GenoGenerationPoems
                .Where(p => p.TreeID == treeId)
                .OrderBy(p => p.GenerationNum)
                .ToListAsync();
        }

        // 查：获取单个字辈详情

        public async Task<GenoGenerationPoem?> GetByIdAsync(Guid id)
        {
            return await _context.GenoGenerationPoems.FindAsync(id);
        }

        // 增：添加新的字辈

        public async Task<bool> CreateAsync(PoemDto dto)
        {
            var poem = new GenoGenerationPoem
            {
                GenerationNum = dto.GenerationNum,
                Word = dto.Word,
                Meaning = dto.Meaning,
                TreeID = dto.TreeId

            };

            _context.GenoGenerationPoems.Add(poem);
            return await _context.SaveChangesAsync() > 0;
        }

        // 改：修改字辈信息

        public async Task<bool> UpdateAsync(PoemDto dto, Guid poemId)
        { 
            var poem = await _context.GenoGenerationPoems.FindAsync(poemId);    
            if (poem == null) return false;
            poem.GenerationNum = dto.GenerationNum;
            poem.Word = dto.Word;
            poem.Meaning = dto.Meaning;

            _context.GenoGenerationPoems.Update(poem);
            return await _context.SaveChangesAsync() > 0;
        }

        // 删：删除字辈

        public async Task<bool> DeleteAsync(Guid id)
        {
            var poem = await _context.GenoGenerationPoems.FindAsync(id);
            if (poem == null) return false;

            _context.GenoGenerationPoems.Remove(poem);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
