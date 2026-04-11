using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 家谱成员新增或修改请求。
    /// </summary>
    public class GenoMemberDto
    {
        [Required]
        public Guid TreeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        public int? GenerationNum { get; set; }

        public Guid? PoemId { get; set; }

        public byte Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(100)]
        public string? BirthDateRaw { get; set; }

        public DateTime? DeathDate { get; set; }

        public bool? IsLiving { get; set; } = true;

        public string? Biography { get; set; }

        public Guid? SysUserId { get; set; }

        public List<Guid> MediaIds { get; set; } = new();
    }

    public class MemberMediaUpdateDto
    {
        [Required]
        public List<Guid> MediaIds { get; set; } = new();
    }

    /// <summary>
    /// 申请把系统账号绑定到家谱成员。
    /// </summary>
    public class MemberIdentifyApplyDto
    {
        [Required]
        public Guid TreeId { get; set; }

        [Required]
        public Guid MemberId { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 树成员统一查询条件。
    /// </summary>
    public class MemberQueryDto
    {
        [Required]
        public Guid TreeId { get; set; }

        public string? Keyword { get; set; }

        public string? Name { get; set; }

        public int? GenerationNum { get; set; }

        public int? GenerationFrom { get; set; }

        public int? GenerationTo { get; set; }

        public Guid? PoemId { get; set; }

        public string? PoemWord { get; set; }

        public byte? Gender { get; set; }

        public bool? IsLiving { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
