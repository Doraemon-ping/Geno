using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    [Table("Geno_Space_Posts")]
    public class GenoSpacePost
    {
        [Key]
        public Guid PostID { get; set; } = Guid.NewGuid();

        public Guid TreeID { get; set; }

        public Guid UserID { get; set; }

        [MaxLength(200)]
        public string? PostTitle { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDel { get; set; }
    }
}
