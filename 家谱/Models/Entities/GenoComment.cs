using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    /// <summary>
    /// 家族空间评论，支持事件、成员、家谱树等对象复用。
    /// </summary>
    [Table("Geno_Comments")]
    public class GenoComment
    {
        [Key]
        public Guid CommentID { get; set; } = Guid.NewGuid();

        public Guid? TreeID { get; set; }

        [Required]
        [MaxLength(50)]
        public string OwnerType { get; set; } = string.Empty;

        public Guid OwnerID { get; set; }

        public Guid? ParentCommentID { get; set; }

        public Guid UserID { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDel { get; set; }
    }
}
