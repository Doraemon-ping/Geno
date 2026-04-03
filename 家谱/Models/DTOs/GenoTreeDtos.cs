using System.ComponentModel.DataAnnotations;

namespace 家谱.Models.DTOs
{
    public class GenoTreeDtos
    {
        [Required(ErrorMessage = "家族名称不能为空")]
        [StringLength(100, ErrorMessage = "名称长度不能超过100个字符")]
        public string TreeName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? AncestorName { get; set; }

        [StringLength(200)]
        public string? Region { get; set; }

        public string? Description { get; set; }
        public Guid Owner { get; set; }

        public bool IsPublic { get; set; } = false;
    }
}
