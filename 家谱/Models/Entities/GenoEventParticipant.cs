using System.ComponentModel.DataAnnotations.Schema;

namespace 家谱.Models.Entities
{
    /// <summary>
    /// 历史事件参与者关联。
    /// </summary>
    [Table("Geno_Event_Participants")]
    public class GenoEventParticipant
    {
        public Guid EventID { get; set; }

        public Guid MemberID { get; set; }

        public string? RoleDescription { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDel { get; set; }
    }
}
