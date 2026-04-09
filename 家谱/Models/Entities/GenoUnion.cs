using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    /// <summary>
    /// 家庭/婚姻单元。
    /// </summary>
    [Table("Geno_Unions")]
    public class GenoUnion
    {
        /// <summary>
        /// 家庭单元主键。
        /// </summary>
        [Key]
        public Guid UnionID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 伴侣 1。
        /// </summary>
        [Required]
        public Guid Partner1ID { get; set; }

        /// <summary>
        /// 伴侣 2。
        /// </summary>
        [Required]
        public Guid Partner2ID { get; set; }

        /// <summary>
        /// 婚姻类型：1-正式结婚，2-事实婚姻，3-离异。
        /// </summary>
        public byte UnionType { get; set; } = 1;

        /// <summary>
        /// 多重婚姻排序。
        /// </summary>
        public int SortOrder { get; set; } = 1;

        /// <summary>
        /// 结婚日期。
        /// </summary>
        public DateTime? MarriageDate { get; set; }

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
