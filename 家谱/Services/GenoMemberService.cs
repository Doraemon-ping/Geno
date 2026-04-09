using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;

namespace 家谱.Services
{
    /// <summary>
    /// 家谱成员服务接口。
    /// </summary>
    public interface IGenoMemberService
    {
        /// <summary>
        /// 获取指定树下的成员列表。
        /// </summary>
        Task<List<GenoMember>> GetByTreeIdAsync(Guid treeId);

        /// <summary>
        /// 根据主键获取成员。
        /// </summary>
        Task<GenoMember?> GetByIdAsync(Guid memberId);

        /// <summary>
        /// 创建树成员。
        /// </summary>
        Task<GenoMember> CreateAsync(GenoMemberDto dto, Guid operatorUserId, Guid? taskId = null);

        /// <summary>
        /// 更新树成员。
        /// </summary>
        Task<bool> UpdateAsync(Guid memberId, GenoMemberDto dto, Guid operatorUserId, Guid? taskId = null);

        /// <summary>
        /// 逻辑删除树成员。
        /// </summary>
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

        public async Task<GenoMember?> GetByIdAsync(Guid memberId)
        {
            return await _db.GenoMembers
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
            await _auditLogService.WriteAsync("Geno_Members", member.MemberID, "CREATE", operatorUserId, new { }, new
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
            }, taskId);
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
