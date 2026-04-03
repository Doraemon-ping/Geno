using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using 家谱.Models.Entities.家谱.Models.Entities;

namespace 家谱.Models.Entities
{

    [Table("Geno_Members")]
    public class GenoMember
    {
        [Key]
        public Guid MemberID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 所属家谱树 ID
        /// </summary>
        [Required]
        public Guid TreeID { get; set; }

        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// 世代（第几代）
        /// </summary>
        public int? GenerationNum { get; set; }

        /// <summary>
        /// 关联字辈 ID（可选）
        /// </summary>
        public Guid? PoemID { get; set; }

        /// <summary>
        /// 关联支系 ID（可选）
        /// </summary>
        public Guid? BranchID { get; set; }

        /// <summary>
        /// 性别：0-未知, 1-男, 2-女
        /// </summary>
        public byte Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// 原始生日描述（如：农历、时辰等文本）
        /// </summary>
        [MaxLength(100)]
        public string? BirthDateRaw { get; set; }

        public DateTime? DeathDate { get; set; }

        /// <summary>
        /// 是否健在
        /// </summary>
        public bool? IsLiving { get; set; } = true;

        /// <summary>
        /// 个人传记
        /// </summary>
        public string? Biography { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 软删除标记
        /// </summary>
        public bool? IsDeleted { get; set; } = false;



    }







}