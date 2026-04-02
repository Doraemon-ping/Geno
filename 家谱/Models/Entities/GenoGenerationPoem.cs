using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    [Table("Geno_Generation_Poems")]
    public class GenoGenerationPoem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid PoemID { get; set; }

        [Required]
        public Guid TreeID { get; set; }

        /// <summary>
        /// 绝对代数（如：第20代）
        /// </summary>
        [Required]
        public int GenerationNum { get; set; }

        /// <summary>
        /// 对应的辈分汉字
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string Word { get; set; } = string.Empty;

        /// <summary>
        /// 字义解释
        /// </summary>
        public string? Meaning { get; set; }

        /// <summary>
        /// 创建时间，映射 SQL 的 DATETIME2
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}