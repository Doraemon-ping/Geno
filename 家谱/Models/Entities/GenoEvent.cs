using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    /// <summary>
    /// 家谱历史事件。
    /// </summary>
    [Table("Geno_Events")]
    public class GenoEvent
    {
        [Key]
        public Guid EventID { get; set; } = Guid.NewGuid();

        public Guid? TreeID { get; set; }

        [Required]
        [MaxLength(200)]
        public string EventTitle { get; set; } = string.Empty;

        public byte EventType { get; set; }

        public bool IsGlobal { get; set; }

        public bool IsPublic { get; set; }

        public DateTime? EventDate { get; set; }

        [MaxLength(100)]
        public string? DateRaw { get; set; }

        public Guid? LocationID { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDel { get; set; }
    }
}
