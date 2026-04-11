using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 历史事件参与者输入项。
    /// </summary>
    public class EventParticipantDto
    {
        [Required(ErrorMessage = "参与成员不能为空")]
        public Guid MemberId { get; set; }

        [MaxLength(100)]
        public string? RoleDescription { get; set; }
    }

    /// <summary>
    /// 历史事件读模型中的参与者。
    /// </summary>
    public class EventParticipantViewDto
    {
        public Guid MemberId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string? RoleDescription { get; set; }
    }

    /// <summary>
    /// 媒体文件读模型。
    /// </summary>
    public class MediaFileDto
    {
        public Guid MediaId { get; set; }

        public Guid? TreeId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string? FileExt { get; set; }

        public string? MimeType { get; set; }

        public long FileSize { get; set; }

        public string? PublicUrl { get; set; }

        public string? Caption { get; set; }

        public int SortOrder { get; set; }

        public byte Status { get; set; }

        public string StatusName { get; set; } = string.Empty;

        public string CreatedAt { get; set; } = string.Empty;
    }

    /// <summary>
    /// 历史事件新增或修改请求。
    /// </summary>
    public class GenoEventDto
    {
        public Guid? TreeId { get; set; }

        [Required(ErrorMessage = "事件标题不能为空")]
        [MaxLength(200)]
        public string EventTitle { get; set; } = string.Empty;

        [Range(1, 255, ErrorMessage = "事件类型不合法")]
        public byte EventType { get; set; }

        public bool IsGlobal { get; set; }

        public bool IsPublic { get; set; }

        public DateTime? EventDate { get; set; }

        [MaxLength(100)]
        public string? DateRaw { get; set; }

        public Guid? LocationId { get; set; }

        public string? Description { get; set; }

        public List<EventParticipantDto> Participants { get; set; } = new();

        public List<Guid> MediaIds { get; set; } = new();
    }

    /// <summary>
    /// 历史事件详情。
    /// </summary>
    public class GenoEventViewDto
    {
        public Guid EventId { get; set; }

        public Guid? TreeId { get; set; }

        public string? TreeName { get; set; }

        public string EventTitle { get; set; } = string.Empty;

        public byte EventType { get; set; }

        public string EventTypeName { get; set; } = string.Empty;

        public bool IsGlobal { get; set; }

        public bool IsPublic { get; set; }

        public DateTime? EventDate { get; set; }

        public string? DateRaw { get; set; }

        public Guid? LocationId { get; set; }

        public string? Description { get; set; }

        public List<EventParticipantViewDto> Participants { get; set; } = new();

        public List<MediaFileDto> MediaFiles { get; set; } = new();
    }
}
