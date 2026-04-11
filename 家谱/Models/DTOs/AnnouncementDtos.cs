using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 公告创建请求。
    /// </summary>
    public class AnnouncementCreateDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Category { get; set; }

        public bool IsPinned { get; set; }

        public bool PublishNow { get; set; } = true;
    }

    /// <summary>
    /// 公告修改请求。
    /// </summary>
    public class AnnouncementUpdateDto : AnnouncementCreateDto
    {
        public byte Status { get; set; } = 1;
    }

    /// <summary>
    /// 公告查询请求。
    /// </summary>
    public class AnnouncementQueryDto
    {
        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;

        public string? Keyword { get; set; }

        public string? Category { get; set; }

        public byte? Status { get; set; }

        public bool PublicOnly { get; set; } = true;
    }

    /// <summary>
    /// 公告展示数据。
    /// </summary>
    public class AnnouncementViewDto
    {
        public Guid AnnouncementId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public byte Status { get; set; }

        public string StatusName { get; set; } = string.Empty;

        public bool IsPinned { get; set; }

        public Guid CreatedBy { get; set; }

        public string CreatorName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }
    }
}
