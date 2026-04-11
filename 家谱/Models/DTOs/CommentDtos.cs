using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    public class CommentCreateDto
    {
        public Guid? TreeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string OwnerType { get; set; } = string.Empty;

        [Required]
        public Guid OwnerId { get; set; }

        public Guid? ParentCommentId { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }

    public class CommentViewDto
    {
        public Guid CommentId { get; set; }

        public Guid? TreeId { get; set; }

        public string OwnerType { get; set; } = string.Empty;

        public Guid OwnerId { get; set; }

        public Guid? ParentCommentId { get; set; }

        public Guid UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public List<CommentViewDto> Replies { get; set; } = new();
    }
}
