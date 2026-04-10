namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 数据库日志展示对象。
    /// </summary>
    public class DataLogDto
    {
        public Guid LogId { get; set; }

        public Guid? TaskId { get; set; }

        public string? ActionCode { get; set; }

        public string? ActionName { get; set; }

        public string TargetTable { get; set; } = string.Empty;

        public string OpType { get; set; } = string.Empty;

        public string OperatorName { get; set; } = string.Empty;

        public string CreatedAt { get; set; } = string.Empty;

        public object? BeforeData { get; set; }

        public object? AfterData { get; set; }
    }

    /// <summary>
    /// 数据库日志查询条件。
    /// </summary>
    public class DataLogQueryDto
    {
        public string? Keyword { get; set; }

        public string? TargetTable { get; set; }

        public string? OpType { get; set; }

        public DateTime? CreatedFrom { get; set; }

        public DateTime? CreatedTo { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
