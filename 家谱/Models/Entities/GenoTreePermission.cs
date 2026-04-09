using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    [Table("Geno_Tree_Permissions")]
    public class GenoTreePermission
    {
        [Key]
        public Guid PermissionID { get; set; } = Guid.NewGuid();

        [Required]
        public Guid TreeID { get; set; }

        [Required]
        public Guid UserID { get; set; }

        [Required]
        public byte RoleType { get; set; }

        public Guid? GrantedBy { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        [ForeignKey(nameof(TreeID))]
        public virtual GenoTree Tree { get; set; } = null!;

        [ForeignKey(nameof(UserID))]
        public virtual SysUser User { get; set; } = null!;
    }
}
