namespace 家谱.Models.DTOs
{
    public class PoemDto
    {
        public Guid? PoemID { get; set; }

        public Guid TreeID { get; set; }

        public int GenerationNum { get; set; }

        public string Word { get; set; } = string.Empty;

        public string? Meaning { get; set; }
    }
}
