namespace 家谱.Models.DTOs
{
    public class PoemDto
    {

        public int GenerationNum { get; set; }

        public string Word { get; set; } = string.Empty;

        public string? Meaning { get; set; }

        public Guid TreeId { get; set; }
    }
}
