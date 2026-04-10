using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Entities;

namespace 家谱.Services
{
    /// <summary>
    /// 家谱成员服务接口。
    /// </summary>
    public interface IGenoMemberService
    {
        Task<List<GenoMember>> GetByTreeIdAsync(Guid treeId);

        Task<PagedResult<GenoMember>> QueryAsync(MemberQueryDto query);

        Task<GenoMember?> GetByIdAsync(Guid memberId);

        Task<GenoMember> CreateAsync(GenoMemberDto dto, Guid operatorUserId, Guid? taskId = null);

        Task<bool> UpdateAsync(Guid memberId, GenoMemberDto dto, Guid operatorUserId, Guid? taskId = null);

        Task<bool> DeleteAsync(Guid memberId, Guid operatorUserId, Guid? taskId = null);
    }

    /// <summary>
    /// 家谱成员服务实现。
    /// </summary>
    public class GenoMemberService : IGenoMemberService
    {
        private readonly GenealogyDbContext _db;
        private readonly IAuditLogService _auditLogService;

        public GenoMemberService(GenealogyDbContext db, IAuditLogService auditLogService)
        {
            _db = db;
            _auditLogService = auditLogService;
        }

        public async Task<List<GenoMember>> GetByTreeIdAsync(Guid treeId)
        {
            return await _db.GenoMembers
                .AsNoTracking()
                .Where(member => member.TreeID == treeId && member.IsDel != true)
                .OrderBy(member => member.GenerationNum)
                .ThenBy(member => member.LastName)
                .ThenBy(member => member.FirstName)
                .ToListAsync();
        }

        public async Task<PagedResult<GenoMember>> QueryAsync(MemberQueryDto query)
        {
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var memberQuery = _db.GenoMembers
                .AsNoTracking()
                .Where(member => member.TreeID == query.TreeId && member.IsDel != true);

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim();
                memberQuery = memberQuery.Where(member =>
                    (member.LastName + member.FirstName).Contains(keyword) ||
                    (member.Biography ?? string.Empty).Contains(keyword) ||
                    (member.BirthDateRaw ?? string.Empty).Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(query.Name))
            {
                var name = query.Name.Trim();
                memberQuery = memberQuery.Where(member => (member.LastName + member.FirstName).Contains(name));
            }

            if (query.GenerationNum.HasValue)
            {
                memberQuery = memberQuery.Where(member => member.GenerationNum == query.GenerationNum.Value);
            }

            if (query.GenerationFrom.HasValue)
            {
                memberQuery = memberQuery.Where(member =>
                    member.GenerationNum.HasValue &&
                    member.GenerationNum.Value >= query.GenerationFrom.Value);
            }

            if (query.GenerationTo.HasValue)
            {
                memberQuery = memberQuery.Where(member =>
                    member.GenerationNum.HasValue &&
                    member.GenerationNum.Value <= query.GenerationTo.Value);
            }

            if (query.PoemId.HasValue)
            {
                memberQuery = memberQuery.Where(member => member.PoemID == query.PoemId.Value);
            }

            if (query.Gender.HasValue)
            {
                memberQuery = memberQuery.Where(member => member.Gender == query.Gender.Value);
            }

            if (query.IsLiving.HasValue)
            {
                memberQuery = memberQuery.Where(member => member.IsLiving == query.IsLiving.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.PoemWord))
            {
                var poemWord = query.PoemWord.Trim();
                var poemIds = await _db.GenoGenerationPoems
                    .AsNoTracking()
                    .Where(poem => poem.TreeID == query.TreeId && !poem.IsDel && poem.Word.Contains(poemWord))
                    .Select(poem => poem.PoemID)
                    .ToListAsync();

                if (poemIds.Count == 0)
                {
                    return new PagedResult<GenoMember>
                    {
                        Items = Array.Empty<GenoMember>(),
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = 0,
                        TotalPages = 0
                    };
                }

                memberQuery = memberQuery.Where(member =>
                    member.PoemID.HasValue &&
                    poemIds.Contains(member.PoemID.Value));
            }

            var totalCount = await memberQuery.CountAsync();
            var items = await memberQuery
                .OrderBy(member => member.GenerationNum ?? int.MaxValue)
                .ThenBy(member => member.LastName)
                .ThenBy(member => member.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<GenoMember>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<GenoMember?> GetByIdAsync(Guid memberId)
        {
            return await _db.GenoMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(member => member.MemberID == memberId && member.IsDel != true);
        }

        public async Task<GenoMember> CreateAsync(GenoMemberDto dto, Guid operatorUserId, Guid? taskId = null)
        {
            var member = new GenoMember
            {
                MemberID = Guid.NewGuid(),
                TreeID = dto.TreeId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                GenerationNum = dto.GenerationNum,
                PoemID = dto.PoemId,
                Gender = dto.Gender,
                BirthDate = dto.BirthDate,
                BirthDateRaw = dto.BirthDateRaw,
                DeathDate = dto.DeathDate,
                IsLiving = dto.IsLiving,
                Biography = dto.Biography,
                CreatedAt = DateTime.UtcNow,
                IsDel = false,
                SysUserId = dto.SysUserId
            };

            _db.GenoMembers.Add(member);
            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Geno_Members",
                member.MemberID,
                "CREATE",
                operatorUserId,
                new { },
                new
                {
                    member.MemberID,
                    member.TreeID,
                    member.LastName,
                    member.FirstName,
                    member.GenerationNum,
                    member.Gender,
                    member.BirthDateRaw,
                    member.Biography,
                    member.SysUserId,
                    member.IsDel
                },
                taskId);

            return member;
        }

        public async Task<bool> UpdateAsync(Guid memberId, GenoMemberDto dto, Guid operatorUserId, Guid? taskId = null)
        {
            var member = await _db.GenoMembers
                .FirstOrDefaultAsync(item => item.MemberID == memberId && item.IsDel != true);

            if (member == null)
            {
                return false;
            }

            var before = BuildMemberLogSnapshot(member);

            member.TreeID = dto.TreeId;
            member.FirstName = dto.FirstName;
            member.LastName = dto.LastName;
            member.GenerationNum = dto.GenerationNum;
            member.PoemID = dto.PoemId;
            member.Gender = dto.Gender;
            member.BirthDate = dto.BirthDate;
            member.BirthDateRaw = dto.BirthDateRaw;
            member.DeathDate = dto.DeathDate;
            member.IsLiving = dto.IsLiving;
            member.Biography = dto.Biography;
            member.SysUserId = dto.SysUserId;

            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Geno_Members",
                member.MemberID,
                "UPDATE",
                operatorUserId,
                before,
                BuildMemberLogSnapshot(member),
                taskId);

            return true;
        }

        public async Task<bool> DeleteAsync(Guid memberId, Guid operatorUserId, Guid? taskId = null)
        {
            var member = await _db.GenoMembers
                .FirstOrDefaultAsync(item => item.MemberID == memberId && item.IsDel != true);

            if (member == null)
            {
                return false;
            }

            var before = BuildMemberLogSnapshot(member);
            member.IsDel = true;

            var childRelations = await _db.GenoUnionMembers
                .IgnoreQueryFilters()
                .Where(item => item.MemberID == memberId && !item.IsDel)
                .ToListAsync();

            foreach (var relation in childRelations)
            {
                relation.IsDel = true;
                relation.UpdatedAt = DateTime.UtcNow;
            }

            var partnerUnions = await _db.GenoUnions
                .IgnoreQueryFilters()
                .Where(item => !item.IsDel && (item.Partner1ID == memberId || item.Partner2ID == memberId))
                .ToListAsync();

            var partnerUnionIds = partnerUnions.Select(item => item.UnionID).ToList();
            var partnerUnionRelations = partnerUnionIds.Count == 0
                ? new List<GenoUnionMember>()
                : await _db.GenoUnionMembers
                    .IgnoreQueryFilters()
                    .Where(item => partnerUnionIds.Contains(item.UnionID) && !item.IsDel)
                    .ToListAsync();

            foreach (var union in partnerUnions)
            {
                union.IsDel = true;
                union.UpdatedAt = DateTime.UtcNow;
            }

            foreach (var relation in partnerUnionRelations)
            {
                relation.IsDel = true;
                relation.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Geno_Members",
                member.MemberID,
                "DELETE",
                operatorUserId,
                before,
                BuildMemberLogSnapshot(member, childRelations.Count, partnerUnions.Count),
                taskId);

            return true;
        }

        private static object BuildMemberLogSnapshot(GenoMember member, int childRelationCount = 0, int partnerUnionCount = 0) => new
        {
            member.MemberID,
            member.TreeID,
            member.LastName,
            member.FirstName,
            member.GenerationNum,
            member.PoemID,
            member.Gender,
            member.BirthDate,
            member.BirthDateRaw,
            member.DeathDate,
            member.IsLiving,
            member.Biography,
            member.SysUserId,
            member.IsDel,
            childRelationCount,
            partnerUnionCount
        };
    }
}
