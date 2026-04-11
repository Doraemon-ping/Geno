using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    /// <summary>
    /// 系统通知公告。
    /// </summary>
    [Table("Sys_Announcements")]
    public class SysAnnouncement
    {
        [Key]
        public Guid AnnouncementID { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Category { get; set; } = "系统公告";

        public byte Status { get; set; } = 1;

        public bool IsPinned { get; set; }

        public Guid CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PublishedAt { get; set; } = DateTime.UtcNow;

        public bool IsDel { get; set; }
    }
}
