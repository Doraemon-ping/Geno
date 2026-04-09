using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 家谱成员新增/修改数据传输对象。
    /// </summary>
    public class GenoMemberDto
    {
        /// <summary>
        /// 所属家谱树标识。
        /// </summary>
        [Required]
        public Guid TreeId { get; set; }

        /// <summary>
        /// 名。
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// 姓。
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// 世代序号。
        /// </summary>
        public int? GenerationNum { get; set; }

        /// <summary>
        /// 关联字辈标识。
        /// </summary>
        public Guid? PoemId { get; set; }

        /// <summary>
        /// 性别：0-未知，1-男，2-女。
        /// </summary>
        public byte Gender { get; set; }

        /// <summary>
        /// 出生日期。
        /// </summary>
        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// 原始出生描述。
        /// </summary>
        [MaxLength(100)]
        public string? BirthDateRaw { get; set; }

        /// <summary>
        /// 去世日期。
        /// </summary>
        public DateTime? DeathDate { get; set; }

        /// <summary>
        /// 是否健在。
        /// </summary>
        public bool? IsLiving { get; set; } = true;

        /// <summary>
        /// 成员简介。
        /// </summary>
        public string? Biography { get; set; }

        /// <summary>
        /// 关联系统用户标识。
        /// </summary>
        public Guid? SysUserId { get; set; }
    }
}
