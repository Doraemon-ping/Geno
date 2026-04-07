using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    /// <summary>
    /// 数据变更历史记录表（快照审计）
    /// </summary>
    [Table("Sys_Data_Logs")]
    public class DataLog
    {
        [Key]
        public Guid LogID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 关联的审核任务 ID（追溯是谁审核通过导致的变更）
        /// </summary>
        public Guid? TaskID { get; set; }

        /// <summary>
        /// 目标表名（如：Geno_Members, Geno_Trees）
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string TargetTable { get; set; } = string.Empty;

        /// <summary>
        /// 目标记录的原始主键 ID
        /// </summary>
        [Required]
        public Guid TargetID { get; set; }

        /// <summary>
        /// 变更前的旧数据全量快照 (JSON)
        /// </summary>
        [Required]
        public string BeforeData { get; set; } = string.Empty;

        /// <summary>
        /// 操作类型：UPDATE, DELETE
        /// </summary>
        [MaxLength(20)]
        public string OpType { get; set; } = "UPDATE";

        /// <summary>
        /// 操作执行人（通常是管理员/审核员）
        /// </summary>
        public Guid? OpUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 导航属性（可选，仅关联审核任务）
        // [ForeignKey(nameof(TaskID))]
        // public virtual ReviewTask? RelatedTask { get; set; }
    }
}
