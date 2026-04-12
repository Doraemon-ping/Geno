using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;
using 家谱.Models.Enums;

namespace 家谱.Services
{
    /// <summary>
    /// 婚姻单元服务接口。
    /// </summary>
    public interface IGenoUnionService
    {
        /// <summary>
        /// 获取树下的婚姻单元列表。
        /// </summary>
        Task<List<GenoUnionViewDto>> GetByTreeIdAsync(Guid treeId);

        /// <summary>
        /// 获取婚姻单元详情。
        /// </summary>
        Task<GenoUnionViewDto?> GetByIdAsync(Guid unionId);

        /// <summary>
        /// 创建婚姻单元。
        /// </summary>
        Task<GenoUnion> CreateAsync(GenoUnionDto dto, Guid operatorUserId, Guid? taskId = null);

        /// <summary>
        /// 修改婚姻单元。
        /// </summary>
        Task<bool> UpdateAsync(Guid unionId, GenoUnionDto dto, Guid operatorUserId, Guid? taskId = null);

        /// <summary>
        /// 逻辑删除婚姻单元。
        /// </summary>
        Task<bool> DeleteAsync(Guid unionId, Guid operatorUserId, Guid? taskId = null);

        /// <summary>
        /// 添加家庭子女关联。
        /// </summary>
        Task<GenoUnionMember> AddMemberAsync(GenoUnionMemberDto dto, Guid operatorUserId, Guid? taskId = null);

        /// <summary>
        /// 删除家庭子女关联。
        /// </summary>
        Task<bool> RemoveMemberAsync(Guid unionId, Guid memberId, Guid operatorUserId, Guid? taskId = null);
    }

    /// <summary>
    /// 婚姻单元服务。
    /// </summary>
    public class GenoUnionService : IGenoUnionService
    {
        private readonly GenealogyDbContext _db;
        private readonly IAuditLogService _auditLogService;

        public GenoUnionService(GenealogyDbContext db, IAuditLogService auditLogService)
        {
            _db = db;
            _auditLogService = auditLogService;
        }

        public async Task<List<GenoUnionViewDto>> GetByTreeIdAsync(Guid treeId)
        {
            var members = await _db.GenoMembers
                .AsNoTracking()
                .Where(member => member.TreeID == treeId)
                .ToListAsync();

            var memberMap = members.ToDictionary(member => member.MemberID);
            if (memberMap.Count == 0)
            {
                return new List<GenoUnionViewDto>();
            }

            var unions = (await _db.GenoUnions
                .AsNoTracking()
                .OrderBy(union => union.SortOrder)
                .ThenBy(union => union.MarriageDate)
                .ThenBy(union => union.CreatedAt)
                .ToListAsync())
                .Where(union => memberMap.ContainsKey(union.Partner1ID) && memberMap.ContainsKey(union.Partner2ID))
                .ToList();

            if (unions.Count == 0)
            {
                return new List<GenoUnionViewDto>();
            }

            var unionIdSet = unions.Select(union => union.UnionID).ToHashSet();
            var unionMembers = (await _db.GenoUnionMembers
                .AsNoTracking()
                .OrderBy(item => item.ChildOrder)
                .ToListAsync())
                .Where(item => unionIdSet.Contains(item.UnionID))
                .ToList();

            return unions
                .Select(union => MapUnionDto(union, treeId, memberMap, unionMembers))
                .ToList();
        }

        public async Task<GenoUnionViewDto?> GetByIdAsync(Guid unionId)
        {
            var union = await _db.GenoUnions
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UnionID == unionId);

            if (union == null)
            {
                return null;
            }

            var unionMembers = await _db.GenoUnionMembers
                .AsNoTracking()
                .Where(item => item.UnionID == unionId)
                .OrderBy(item => item.ChildOrder)
                .ToListAsync();

            var partner1 = await _db.GenoMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(member => member.MemberID == union.Partner1ID);

            var partner2 = await _db.GenoMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(member => member.MemberID == union.Partner2ID);

            if (partner1 == null || partner2 == null)
            {
                return null;
            }

            var members = await _db.GenoMembers
                .AsNoTracking()
                .Where(member => member.TreeID == partner1.TreeID)
                .ToListAsync();

            var memberMap = members.ToDictionary(member => member.MemberID);
            if (!memberMap.ContainsKey(union.Partner1ID) || !memberMap.ContainsKey(union.Partner2ID))
            {
                return null;
            }

            return MapUnionDto(union, partner1.TreeID, memberMap, unionMembers);
        }

        public async Task<GenoUnion> CreateAsync(GenoUnionDto dto, Guid operatorUserId, Guid? taskId = null)
        {
            var (partner1, partner2) = await ValidatePartnersAsync(dto.TreeId, dto.Partner1Id, dto.Partner2Id);

            var exists = await _db.GenoUnions
                .IgnoreQueryFilters()
                .AnyAsync(union =>
                    !union.IsDel &&
                    union.Partner1ID == dto.Partner1Id &&
                    union.Partner2ID == dto.Partner2Id &&
                    union.SortOrder == dto.SortOrder);

            if (exists)
            {
                throw new InvalidOperationException("该婚姻单元已存在，请勿重复创建");
            }

            var union = new GenoUnion
            {
                UnionID = Guid.NewGuid(),
                Partner1ID = dto.Partner1Id,
                Partner2ID = dto.Partner2Id,
                UnionType = dto.UnionType,
                SortOrder = dto.SortOrder <= 0 ? 1 : dto.SortOrder,
                MarriageDate = dto.MarriageDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDel = false
            };

            _db.GenoUnions.Add(union);
            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Geno_Unions",
                union.UnionID,
                "CREATE",
                operatorUserId,
                new { },
                BuildUnionLogSnapshot(union, dto.TreeId, partner1, partner2),
                taskId);

            return union;
        }

        public async Task<bool> UpdateAsync(Guid unionId, GenoUnionDto dto, Guid operatorUserId, Guid? taskId = null)
        {
            var union = await _db.GenoUnions
                .FirstOrDefaultAsync(item => item.UnionID == unionId && !item.IsDel);

            if (union == null)
            {
                return false;
            }

            var (partner1, partner2) = await ValidatePartnersAsync(dto.TreeId, dto.Partner1Id, dto.Partner2Id);
            var exists = await _db.GenoUnions
                .IgnoreQueryFilters()
                .AnyAsync(item =>
                    item.UnionID != unionId &&
                    !item.IsDel &&
                    item.Partner1ID == dto.Partner1Id &&
                    item.Partner2ID == dto.Partner2Id &&
                    item.SortOrder == dto.SortOrder);

            if (exists)
            {
                throw new InvalidOperationException("该婚姻单元已存在，请勿重复修改");
            }

            var beforePartners = await _db.GenoMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(member => member.MemberID == union.Partner1ID || member.MemberID == union.Partner2ID)
                .ToListAsync();
            var beforePartnerMap = beforePartners.ToDictionary(member => member.MemberID);

            if (!beforePartnerMap.TryGetValue(union.Partner1ID, out var beforePartner1) ||
                !beforePartnerMap.TryGetValue(union.Partner2ID, out var beforePartner2))
            {
                throw new InvalidOperationException("婚姻单元数据不完整，无法修改");
            }

            var before = BuildUnionLogSnapshot(union, dto.TreeId, beforePartner1, beforePartner2);

            union.Partner1ID = dto.Partner1Id;
            union.Partner2ID = dto.Partner2Id;
            union.UnionType = dto.UnionType;
            union.SortOrder = dto.SortOrder <= 0 ? 1 : dto.SortOrder;
            union.MarriageDate = dto.MarriageDate;
            union.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Geno_Unions",
                union.UnionID,
                "UPDATE",
                operatorUserId,
                before,
                BuildUnionLogSnapshot(union, dto.TreeId, partner1, partner2),
                taskId);

            return true;
        }

        public async Task<bool> DeleteAsync(Guid unionId, Guid operatorUserId, Guid? taskId = null)
        {
            var union = await _db.GenoUnions
                .FirstOrDefaultAsync(item => item.UnionID == unionId);

            if (union == null)
            {
                return false;
            }

            var partnerIds = new[] { union.Partner1ID, union.Partner2ID };
            var partners = await _db.GenoMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(member => partnerIds.Contains(member.MemberID))
                .ToListAsync();

            var partnerMap = partners.ToDictionary(member => member.MemberID);
            if (!partnerMap.TryGetValue(union.Partner1ID, out var partner1) || !partnerMap.TryGetValue(union.Partner2ID, out var partner2))
            {
                throw new InvalidOperationException("婚姻单元数据不完整，无法删除");
            }

            var treeId = partner1.TreeID;
            var before = BuildUnionLogSnapshot(union, treeId, partner1, partner2);

            union.IsDel = true;
            union.UpdatedAt = DateTime.UtcNow;

            var relations = await _db.GenoUnionMembers
                .IgnoreQueryFilters()
                .Where(item => item.UnionID == unionId && !item.IsDel)
                .ToListAsync();

            foreach (var relation in relations)
            {
                relation.IsDel = true;
                relation.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Geno_Unions",
                union.UnionID,
                "DELETE",
                operatorUserId,
                before,
                BuildUnionLogSnapshot(union, treeId, partner1, partner2),
                taskId);

            return true;
        }

        public async Task<GenoUnionMember> AddMemberAsync(GenoUnionMemberDto dto, Guid operatorUserId, Guid? taskId = null)
        {
            var union = await _db.GenoUnions
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UnionID == dto.UnionId)
                ?? throw new KeyNotFoundException("婚姻单元不存在");

            var child = await _db.GenoMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(member => member.MemberID == dto.MemberId && member.IsDel != true)
                ?? throw new KeyNotFoundException("目标成员不存在");

            var (partner1, partner2) = await ValidatePartnersAsync(dto.TreeId, union.Partner1ID, union.Partner2ID);

            if (child.TreeID != dto.TreeId)
            {
                throw new InvalidOperationException("目标成员不属于当前家谱树");
            }

            if (dto.MemberId == union.Partner1ID || dto.MemberId == union.Partner2ID)
            {
                throw new InvalidOperationException("伴侣不能再作为该家庭的子女成员");
            }

            var existing = await _db.GenoUnionMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.UnionID == dto.UnionId && item.MemberID == dto.MemberId);

            var opType = "CREATE";
            object before = new { };

            if (existing == null)
            {
                existing = new GenoUnionMember
                {
                    UnionID = dto.UnionId,
                    MemberID = dto.MemberId,
                    RelType = dto.RelType,
                    ChildOrder = dto.ChildOrder <= 0 ? 1 : dto.ChildOrder,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDel = false
                };

                _db.GenoUnionMembers.Add(existing);
            }
            else
            {
                opType = existing.IsDel ? "CREATE" : "UPDATE";
                before = BuildUnionMemberLogSnapshot(existing, child, union, dto.TreeId, partner1, partner2);
                existing.RelType = dto.RelType;
                existing.ChildOrder = dto.ChildOrder <= 0 ? 1 : dto.ChildOrder;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDel = false;
            }

            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Geno_Union_Members",
                dto.MemberId,
                opType,
                operatorUserId,
                before,
                BuildUnionMemberLogSnapshot(existing, child, union, dto.TreeId, partner1, partner2),
                taskId);

            return existing;
        }

        public async Task<bool> RemoveMemberAsync(Guid unionId, Guid memberId, Guid operatorUserId, Guid? taskId = null)
        {
            var relation = await _db.GenoUnionMembers
                .FirstOrDefaultAsync(item => item.UnionID == unionId && item.MemberID == memberId);

            if (relation == null)
            {
                return false;
            }

            var union = await _db.GenoUnions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UnionID == unionId)
                ?? throw new KeyNotFoundException("婚姻单元不存在");

            var memberIds = new[] { union.Partner1ID, union.Partner2ID, memberId };
            var members = await _db.GenoMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(member => memberIds.Contains(member.MemberID))
                .ToListAsync();

            var memberMap = members.ToDictionary(member => member.MemberID);
            if (!memberMap.TryGetValue(union.Partner1ID, out var partner1) ||
                !memberMap.TryGetValue(union.Partner2ID, out var partner2) ||
                !memberMap.TryGetValue(memberId, out var child))
            {
                throw new InvalidOperationException("家庭成员信息不完整，无法删除关联");
            }

            var treeId = partner1.TreeID;
            var before = BuildUnionMemberLogSnapshot(relation, child, union, treeId, partner1, partner2);

            relation.IsDel = true;
            relation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(
                "Geno_Union_Members",
                memberId,
                "DELETE",
                operatorUserId,
                before,
                BuildUnionMemberLogSnapshot(relation, child, union, treeId, partner1, partner2),
                taskId);

            return true;
        }

        private async Task<(GenoMember Partner1, GenoMember Partner2)> ValidatePartnersAsync(Guid treeId, Guid partner1Id, Guid partner2Id)
        {
            if (partner1Id == Guid.Empty || partner2Id == Guid.Empty)
            {
                throw new ArgumentException("伴侣成员不能为空");
            }

            if (partner1Id == partner2Id)
            {
                throw new ArgumentException("家庭单元中的两个伴侣不能是同一成员");
            }

            var partnerIds = new[] { partner1Id, partner2Id };
            var partners = await _db.GenoMembers
                .IgnoreQueryFilters()
                .Where(member => partnerIds.Contains(member.MemberID) && member.IsDel != true)
                .ToListAsync();

            if (partners.Count != 2)
            {
                throw new KeyNotFoundException("家庭单元的伴侣成员不存在");
            }

            var partner1 = partners.First(member => member.MemberID == partner1Id);
            var partner2 = partners.First(member => member.MemberID == partner2Id);

            if (partner1.TreeID != treeId || partner2.TreeID != treeId)
            {
                throw new InvalidOperationException("伴侣成员必须属于同一棵家谱树");
            }

            return (partner1, partner2);
        }

        private static GenoUnionViewDto MapUnionDto(
            GenoUnion union,
            Guid treeId,
            IReadOnlyDictionary<Guid, GenoMember> memberMap,
            IReadOnlyCollection<GenoUnionMember> unionMembers)
        {
            if (!memberMap.TryGetValue(union.Partner1ID, out var partner1) || !memberMap.TryGetValue(union.Partner2ID, out var partner2))
            {
                throw new InvalidOperationException("婚姻单元缺少伴侣成员信息");
            }

            var children = unionMembers
                .Where(item => item.UnionID == union.UnionID)
                .OrderBy(item => item.ChildOrder)
                .Select(item =>
                {
                    if (!memberMap.TryGetValue(item.MemberID, out var child))
                    {
                        return null;
                    }

                    return new UnionChildSummaryDto
                    {
                        MemberId = child.MemberID,
                        FullName = $"{child.LastName}{child.FirstName}",
                        GenerationNum = child.GenerationNum,
                        Gender = child.Gender,
                        GenderName = GetGenderName(child.Gender),
                        BirthDateRaw = child.BirthDateRaw,
                        RelType = item.RelType,
                        RelTypeName = ReviewActions.GetUnionMemberRelationDisplayName(item.RelType),
                        ChildOrder = item.ChildOrder
                    };
                })
                .Where(item => item != null)
                .Cast<UnionChildSummaryDto>()
                .ToList();

            return new GenoUnionViewDto
            {
                UnionId = union.UnionID,
                TreeId = treeId,
                UnionType = union.UnionType,
                UnionTypeName = ReviewActions.GetUnionTypeDisplayName(union.UnionType),
                SortOrder = union.SortOrder,
                MarriageDate = union.MarriageDate,
                Partner1 = MapPerson(partner1),
                Partner2 = MapPerson(partner2),
                Children = children
            };
        }

        private static UnionPersonSummaryDto MapPerson(GenoMember member)
        {
            return new UnionPersonSummaryDto
            {
                MemberId = member.MemberID,
                FullName = $"{member.LastName}{member.FirstName}",
                GenerationNum = member.GenerationNum,
                Gender = member.Gender,
                GenderName = GetGenderName(member.Gender),
                BirthDateRaw = member.BirthDateRaw
            };
        }

        private static string GetGenderName(byte gender) => gender switch
        {
            1 => "男",
            2 => "女",
            _ => "未知"
        };

        private static object BuildUnionLogSnapshot(GenoUnion union, Guid treeId, GenoMember partner1, GenoMember partner2)
        {
            return new
            {
                union.UnionID,
                TreeID = treeId,
                union.Partner1ID,
                Partner1Name = $"{partner1.LastName}{partner1.FirstName}",
                union.Partner2ID,
                Partner2Name = $"{partner2.LastName}{partner2.FirstName}",
                union.UnionType,
                UnionTypeName = ReviewActions.GetUnionTypeDisplayName(union.UnionType),
                union.SortOrder,
                union.MarriageDate,
                union.IsDel
            };
        }

        private static object BuildUnionMemberLogSnapshot(
            GenoUnionMember relation,
            GenoMember child,
            GenoUnion union,
            Guid treeId,
            GenoMember partner1,
            GenoMember partner2)
        {
            return new
            {
                relation.UnionID,
                TreeID = treeId,
                relation.MemberID,
                ChildName = $"{child.LastName}{child.FirstName}",
                relation.RelType,
                RelTypeName = ReviewActions.GetUnionMemberRelationDisplayName(relation.RelType),
                relation.ChildOrder,
                relation.IsDel,
                union.Partner1ID,
                Partner1Name = $"{partner1.LastName}{partner1.FirstName}",
                union.Partner2ID,
                Partner2Name = $"{partner2.LastName}{partner2.FirstName}"
            };
        }
    }
}
