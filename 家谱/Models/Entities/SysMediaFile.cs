using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    /// <summary>
    /// 通用媒体文件。
    /// </summary>
    [Table("Sys_Media_Files")]
    public class SysMediaFile
    {
        [Key]
        public Guid MediaID { get; set; } = Guid.NewGuid();

        public Guid? TreeID { get; set; }

        [Required]
        [MaxLength(50)]
        public string OwnerType { get; set; } = string.Empty;

        public Guid? OwnerID { get; set; }

        [Required]
        [MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? FileExt { get; set; }

        [MaxLength(100)]
        public string? MimeType { get; set; }

        public long FileSize { get; set; }

        [Required]
        [MaxLength(500)]
        public string StoragePath { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? PublicUrl { get; set; }

        [MaxLength(128)]
        public string? HashValue { get; set; }

        [MaxLength(200)]
        public string? Caption { get; set; }

        public int SortOrder { get; set; } = 1;

        public Guid UploadUserID { get; set; }

        public Guid? ReviewTaskID { get; set; }

        public byte Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDel { get; set; }
    }
}
