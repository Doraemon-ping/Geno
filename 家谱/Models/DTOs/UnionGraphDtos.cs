namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 婚姻单元图整体数据。
    /// </summary>
    public class UnionGraphDto
    {
        public Guid TreeId { get; set; }

        public string TreeName { get; set; } = string.Empty;

        public int MemberCount { get; set; }

        public int UnionCount { get; set; }

        public int GenerationCount { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public List<UnionGraphNodeDto> Nodes { get; set; } = new();

        public List<UnionGraphEdgeDto> Edges { get; set; } = new();
    }

    /// <summary>
    /// 婚姻单元图节点。
    /// </summary>
    public class UnionGraphNodeDto
    {
        public string Id { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public Guid? MemberId { get; set; }

        public Guid? UnionId { get; set; }

        public string Label { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public int Generation { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    /// <summary>
    /// 婚姻单元图连线。
    /// </summary>
    public class UnionGraphEdgeDto
    {
        public string FromId { get; set; } = string.Empty;

        public string ToId { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string? Label { get; set; }
    }
}
