using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    /// <summary>
    /// 家庭单元中的子女关系。
    /// </summary>
    [Table("Geno_Union_Members")]
    public class GenoUnionMember
    {
        /// <summary>
        /// 家庭单元标识。
        /// </summary>
        public Guid UnionID { get; set; }

        /// <summary>
        /// 子女成员标识。
        /// </summary>
        public Guid MemberID { get; set; }

        /// <summary>
        /// 关系类型：1-亲生，2-收养，3-继子，4-过继。
        /// </summary>
        public byte RelType { get; set; } = 1;

        /// <summary>
        /// 在该家庭中的排行。
        /// </summary>
        public int ChildOrder { get; set; } = 1;

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间。
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 逻辑删除标记。
        /// </summary>
        public bool IsDel { get; set; }
    }
}
