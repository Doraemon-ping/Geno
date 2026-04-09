using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Enums;

namespace 家谱.Services
{
    /// <summary>
    /// 婚姻单元树图服务接口。
    /// </summary>
    public interface IUnionGraphService
    {
        /// <summary>
        /// 构建指定家谱树的婚姻单元图。
        /// </summary>
        Task<UnionGraphDto> BuildTreeGraphAsync(Guid treeId);
    }

    /// <summary>
    /// 将成员、婚姻单元和子女关系整理为浏览器可直接渲染的图数据。
    /// </summary>
    public class UnionGraphService : IUnionGraphService
    {
        private const double MemberWidth = 156;
        private const double MemberHeight = 64;
        private const double UnionWidth = 88;
        private const double UnionHeight = 28;
        private const double HorizontalGap = 54;
        private const double MemberVerticalGap = 184;
        private const double UnionVerticalOffset = 96;
        private const double Padding = 80;

        private readonly GenealogyDbContext _db;

        public UnionGraphService(GenealogyDbContext db)
        {
            _db = db;
        }

        public async Task<UnionGraphDto> BuildTreeGraphAsync(Guid treeId)
        {
            var tree = await _db.GenoTrees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TreeID == treeId)
                ?? throw new KeyNotFoundException("家谱树不存在");

            var members = await _db.GenoMembers
                .AsNoTracking()
                .Where(member => member.TreeID == treeId)
                .OrderBy(member => member.GenerationNum)
                .ThenBy(member => member.LastName)
                .ThenBy(member => member.FirstName)
                .ToListAsync();

            if (members.Count == 0)
            {
                return new UnionGraphDto
                {
                    TreeId = tree.TreeID,
                    TreeName = tree.TreeName,
                    MemberCount = 0,
                    UnionCount = 0,
                    GenerationCount = 0,
                    Width = 1200,
                    Height = 400
                };
            }

            var memberMap = members.ToDictionary(member => member.MemberID);
            var memberIds = memberMap.Keys.ToHashSet();

            var unionsQuery = _db.GenoUnions
                .AsNoTracking()
                .Where(union => memberIds.Contains(union.Partner1ID) && memberIds.Contains(union.Partner2ID));

            var unions = await unionsQuery
                .OrderBy(union => union.SortOrder)
                .ThenBy(union => union.MarriageDate)
                .ThenBy(union => union.CreatedAt)
                .ToListAsync();

            var unionMembers = await (
                from relation in _db.GenoUnionMembers.AsNoTracking()
                join union in unionsQuery on relation.UnionID equals union.UnionID
                orderby relation.ChildOrder, relation.MemberID
                select relation
            ).ToListAsync();

            var relationMemberIds = unionMembers.Select(item => item.MemberID).ToHashSet();
            var roots = members
                .Where(member => !relationMemberIds.Contains(member.MemberID))
                .OrderBy(member => member.GenerationNum ?? int.MaxValue)
                .ThenBy(member => member.LastName)
                .ThenBy(member => member.FirstName)
                .ToList();

            if (roots.Count == 0)
            {
                roots = members
                    .OrderBy(member => member.GenerationNum ?? int.MaxValue)
                    .ThenBy(member => member.LastName)
                    .ThenBy(member => member.FirstName)
                    .Take(1)
                    .ToList();
            }

            var partnerUnions = unions
                .SelectMany(union => new[]
                {
                    new { MemberId = union.Partner1ID, Union = union },
                    new { MemberId = union.Partner2ID, Union = union }
                })
                .GroupBy(item => item.MemberId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Union)
                        .OrderBy(item => item.SortOrder)
                        .ThenBy(item => item.MarriageDate)
                        .ThenBy(item => item.CreatedAt)
                        .ToList());

            var childrenByUnion = unionMembers
                .GroupBy(item => item.UnionID)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(item => item.ChildOrder).ThenBy(item => item.MemberID).ToList());

            var memberOrder = new Dictionary<Guid, double>();
            var visitedUnions = new HashSet<Guid>();
            var orderSeed = 0d;

            void AssignMember(Guid memberId)
            {
                if (!memberOrder.ContainsKey(memberId))
                {
                    memberOrder[memberId] = orderSeed;
                    orderSeed += 1d;
                }

                if (!partnerUnions.TryGetValue(memberId, out var relatedUnions))
                {
                    return;
                }

                foreach (var union in relatedUnions)
                {
                    if (!visitedUnions.Add(union.UnionID))
                    {
                        continue;
                    }

                    var orderedPartners = new[] { union.Partner1ID, union.Partner2ID }
                        .OrderBy(id => id == memberId ? 0 : 1)
                        .ToList();

                    foreach (var partnerId in orderedPartners)
                    {
                        if (!memberOrder.ContainsKey(partnerId))
                        {
                            memberOrder[partnerId] = orderSeed;
                            orderSeed += 1d;
                        }
                    }

                    if (!childrenByUnion.TryGetValue(union.UnionID, out var children))
                    {
                        continue;
                    }

                    foreach (var child in children)
                    {
                        if (!memberOrder.ContainsKey(child.MemberID))
                        {
                            memberOrder[child.MemberID] = orderSeed;
                            orderSeed += 1d;
                        }

                        AssignMember(child.MemberID);
                    }
                }
            }

            foreach (var root in roots)
            {
                AssignMember(root.MemberID);
            }

            foreach (var member in members)
            {
                AssignMember(member.MemberID);
            }

            var generationMap = members.ToDictionary(
                member => member.MemberID,
                member => Math.Max(member.GenerationNum ?? 1, 1));

            var memberLayers = members
                .GroupBy(member => generationMap[member.MemberID])
                .OrderBy(group => group.Key)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(member => memberOrder[member.MemberID]).ToList());

            var nodes = new List<UnionGraphNodeDto>();
            var edges = new List<UnionGraphEdgeDto>();
            var memberNodeMap = new Dictionary<Guid, UnionGraphNodeDto>();

            foreach (var layer in memberLayers)
            {
                for (var index = 0; index < layer.Value.Count; index++)
                {
                    var member = layer.Value[index];
                    var node = new UnionGraphNodeDto
                    {
                        Id = $"member-{member.MemberID}",
                        Kind = "member",
                        MemberId = member.MemberID,
                        Label = $"{member.LastName}{member.FirstName}",
                        Subtitle = $"第{layer.Key}代 · {GetGenderName(member.Gender)}",
                        Generation = layer.Key,
                        X = Padding + index * (MemberWidth + HorizontalGap),
                        Y = Padding + (layer.Key - 1) * MemberVerticalGap,
                        Width = MemberWidth,
                        Height = MemberHeight
                    };

                    nodes.Add(node);
                    memberNodeMap[member.MemberID] = node;
                }
            }

            foreach (var union in unions)
            {
                if (!memberNodeMap.TryGetValue(union.Partner1ID, out var partner1Node) ||
                    !memberNodeMap.TryGetValue(union.Partner2ID, out var partner2Node))
                {
                    continue;
                }

                var unionGeneration = Math.Min(generationMap[union.Partner1ID], generationMap[union.Partner2ID]);
                var unionNode = new UnionGraphNodeDto
                {
                    Id = $"union-{union.UnionID}",
                    Kind = "union",
                    UnionId = union.UnionID,
                    Label = ReviewActions.GetUnionTypeDisplayName(union.UnionType),
                    Subtitle = union.MarriageDate?.ToString("yyyy-MM-dd") ?? $"第 {union.SortOrder} 任",
                    Generation = unionGeneration,
                    X = ((partner1Node.X + partner1Node.Width / 2d) + (partner2Node.X + partner2Node.Width / 2d)) / 2d - UnionWidth / 2d,
                    Y = Padding + (unionGeneration - 1) * MemberVerticalGap + UnionVerticalOffset,
                    Width = UnionWidth,
                    Height = UnionHeight
                };

                nodes.Add(unionNode);

                edges.Add(new UnionGraphEdgeDto
                {
                    FromId = partner1Node.Id,
                    ToId = unionNode.Id,
                    Kind = "partner"
                });
                edges.Add(new UnionGraphEdgeDto
                {
                    FromId = partner2Node.Id,
                    ToId = unionNode.Id,
                    Kind = "partner"
                });

                if (!childrenByUnion.TryGetValue(union.UnionID, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    if (!memberNodeMap.TryGetValue(child.MemberID, out var childNode))
                    {
                        continue;
                    }

                    edges.Add(new UnionGraphEdgeDto
                    {
                        FromId = unionNode.Id,
                        ToId = childNode.Id,
                        Kind = "child",
                        Label = ReviewActions.GetUnionMemberRelationDisplayName(child.RelType)
                    });
                }
            }

            var width = nodes.Count == 0 ? 1200 : nodes.Max(node => node.X + node.Width) + Padding;
            var height = nodes.Count == 0 ? 600 : nodes.Max(node => node.Y + node.Height) + Padding;

            return new UnionGraphDto
            {
                TreeId = tree.TreeID,
                TreeName = tree.TreeName,
                MemberCount = members.Count,
                UnionCount = unions.Count,
                GenerationCount = memberLayers.Keys.Count,
                Width = width,
                Height = height,
                Nodes = nodes
                    .OrderBy(node => node.Kind == "union" ? 1 : 0)
                    .ThenBy(node => node.Y)
                    .ThenBy(node => node.X)
                    .ToList(),
                Edges = edges
            };
        }

        private static string GetGenderName(byte gender) => gender switch
        {
            1 => "男",
            2 => "女",
            _ => "未知"
        };
    }
}
