namespace 家谱.Models.Entities
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    // 家谱树实体，代表一棵家谱树 ==

    [Table("Geno_Trees")]
    public class GenoTree
    {
        [Key]
        public Guid TreeID { get; set; } = Guid.NewGuid();

        //家族名

        [Required, StringLength(100)]
        public string TreeName { get; set; } = string.Empty;

        //始祖

        public string? AncestorName { get; set; }

        //发源地

        public string? Region { get; set; }

        //简介

        public string? Description { get; set; }

        // 权限核心：谁创建了这棵树

        public Guid OwnerID { get; set; }

        //公开？

        public bool IsPublic { get; set; } = false;

        public DateTime CreateTime { get; set; } = DateTime.UtcNow;

        // 导航属性：一棵树对应多个字辈
        public ICollection<GenoGenerationPoem>? Poems { get; set; }
    }
}
