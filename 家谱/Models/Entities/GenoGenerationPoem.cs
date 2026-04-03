namespace 家谱.Models.Entities
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("Geno_Generation_Poems")]
    public class GenoGenerationPoem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid PoemID { get; set; } = Guid.NewGuid();


        [Required]
        public Guid TreeID { get; set; }

        [Required]
        public int GenerationNum { get; set; }

        [Required]
        [MaxLength(10)]
        public string Word { get; set; } = string.Empty;

        public string? Meaning { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
