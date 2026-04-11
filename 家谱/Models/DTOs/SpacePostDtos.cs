using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    public class SpacePostCreateDto
    {
        [Required]
        public Guid TreeId { get; set; }

        [MaxLength(200)]
        public string? PostTitle { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public List<Guid> MediaIds { get; set; } = new();
    }

    public class SpacePostUpdateDto
    {
        [MaxLength(200)]
        public string? PostTitle { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public List<Guid> MediaIds { get; set; } = new();
    }

    public class SpacePostViewDto
    {
        public Guid PostId { get; set; }

        public Guid TreeId { get; set; }

        public Guid UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public string? PostTitle { get; set; }

        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public List<MediaFileDto> MediaFiles { get; set; } = new();
    }
}
