using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;

namespace 家谱.Services
{
    public interface IGenoPoemService
    {
        Task<List<GenoGenerationPoem>> GetByTreeIdAsync(Guid treeId);

        Task<GenoGenerationPoem?> GetByIdAsync(Guid id);

        Task<GenoGenerationPoem> CreateAsync(PoemDto dto, Guid operatorUserId, Guid? taskId = null);

        Task<bool> UpdateAsync(PoemDto dto, Guid poemId, Guid operatorUserId, Guid? taskId = null);

        Task<bool> DeleteAsync(Guid id, Guid operatorUserId, Guid? taskId = null);
    }

    public class GenoPoemService : IGenoPoemService
    {
        private readonly GenealogyDbContext _context;
        private readonly IAuditLogService _auditLogService;

        public GenoPoemService(GenealogyDbContext context, IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
        }

        public async Task<List<GenoGenerationPoem>> GetByTreeIdAsync(Guid treeId)
        {
            return await _context.GenoGenerationPoems
                .Where(p => p.TreeID == treeId && !p.IsDel)
                .OrderBy(p => p.GenerationNum)
                .ToListAsync();
        }

        public async Task<GenoGenerationPoem?> GetByIdAsync(Guid id)
        {
            return await _context.GenoGenerationPoems
                .FirstOrDefaultAsync(p => p.PoemID == id && !p.IsDel);
        }

        public async Task<GenoGenerationPoem> CreateAsync(PoemDto dto, Guid operatorUserId, Guid? taskId = null)
        {
            var poem = new GenoGenerationPoem
            {
                PoemID = Guid.NewGuid(),
                GenerationNum = dto.GenerationNum,
                Word = dto.Word,
                Meaning = dto.Meaning,
                TreeID = dto.TreeId,
                CreatedAt = DateTime.UtcNow,
                IsDel = false
            };

            _context.GenoGenerationPoems.Add(poem);
            await _context.SaveChangesAsync();
            await _auditLogService.WriteAsync("Geno_Generation_Poems", poem.PoemID, "CREATE", operatorUserId, new { }, taskId);
            return poem;
        }

        public async Task<bool> UpdateAsync(PoemDto dto, Guid poemId, Guid operatorUserId, Guid? taskId = null)
        {
            var poem = await _context.GenoGenerationPoems.FirstOrDefaultAsync(p => p.PoemID == poemId && !p.IsDel);
            if (poem == null)
            {
                return false;
            }

            var before = new
            {
                poem.PoemID,
                poem.TreeID,
                poem.GenerationNum,
                poem.Word,
                poem.Meaning
            };

            poem.GenerationNum = dto.GenerationNum;
            poem.Word = dto.Word;
            poem.Meaning = dto.Meaning;

            await _context.SaveChangesAsync();
            await _auditLogService.WriteAsync("Geno_Generation_Poems", poem.PoemID, "UPDATE", operatorUserId, before, taskId);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id, Guid operatorUserId, Guid? taskId = null)
        {
            var poem = await _context.GenoGenerationPoems.FirstOrDefaultAsync(p => p.PoemID == id && !p.IsDel);
            if (poem == null)
            {
                return false;
            }

            var before = new
            {
                poem.PoemID,
                poem.TreeID,
                poem.GenerationNum,
                poem.Word,
                poem.Meaning
            };

            poem.IsDel = true;
            await _context.SaveChangesAsync();
            await _auditLogService.WriteAsync("Geno_Generation_Poems", poem.PoemID, "DELETE", operatorUserId, before, taskId);
            return true;
        }
    }
}
