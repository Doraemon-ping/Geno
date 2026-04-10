using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 婚姻单元创建或修改请求。
    /// </summary>
    public class GenoUnionDto
    {
        /// <summary>
        /// 所属家谱树。
        /// </summary>
        [Required]
        public Guid TreeId { get; set; }

        /// <summary>
        /// 伴侣 1。
        /// </summary>
        [Required]
        public Guid Partner1Id { get; set; }

        /// <summary>
        /// 伴侣 2。
        /// </summary>
        [Required]
        public Guid Partner2Id { get; set; }

        /// <summary>
        /// 婚姻类型：1-正式结婚，2-事实婚姻，3-离异。
        /// </summary>
        public byte UnionType { get; set; } = 1;

        /// <summary>
        /// 婚姻排序。
        /// </summary>
        public int SortOrder { get; set; } = 1;

        /// <summary>
        /// 结婚日期。
        /// </summary>
        public DateTime? MarriageDate { get; set; }
    }

    /// <summary>
    /// 家庭子女关联请求。
    /// </summary>
    public class GenoUnionMemberDto
    {
        /// <summary>
        /// 所属家谱树。
        /// </summary>
        [Required]
        public Guid TreeId { get; set; }

        /// <summary>
        /// 家庭单元标识。
        /// </summary>
        [Required]
        public Guid UnionId { get; set; }

        /// <summary>
        /// 子女成员标识。
        /// </summary>
        [Required]
        public Guid MemberId { get; set; }

        /// <summary>
        /// 关系类型：1-亲生，2-收养，3-继子，4-过继。
        /// </summary>
        public byte RelType { get; set; } = 1;

        /// <summary>
        /// 子女排行。
        /// </summary>
        public int ChildOrder { get; set; } = 1;
    }

    /// <summary>
    /// 家庭单元中的成员摘要。
    /// </summary>
    public class UnionPersonSummaryDto
    {
        public Guid MemberId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public int? GenerationNum { get; set; }

        public byte Gender { get; set; }

        public string GenderName { get; set; } = "未知";

        public string? BirthDateRaw { get; set; }
    }

    /// <summary>
    /// 家庭中的子女摘要。
    /// </summary>
    public class UnionChildSummaryDto : UnionPersonSummaryDto
    {
        public byte RelType { get; set; }

        public string RelTypeName { get; set; } = "亲生";

        public int ChildOrder { get; set; }
    }

    /// <summary>
    /// 婚姻单元展示对象。
    /// </summary>
    public class GenoUnionViewDto
    {
        public Guid UnionId { get; set; }

        public Guid TreeId { get; set; }

        public byte UnionType { get; set; }

        public string UnionTypeName { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public DateTime? MarriageDate { get; set; }

        public UnionPersonSummaryDto Partner1 { get; set; } = new();

        public UnionPersonSummaryDto Partner2 { get; set; } = new();

        public List<UnionChildSummaryDto> Children { get; set; } = new();
    }
}
