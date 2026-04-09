namespace 家谱.Models.DTOs
{
    /// <summary>
    /// 数据库操作日志展示对象。
    /// </summary>
    public class DataLogDto
    {
        public Guid LogId { get; set; }

        public Guid? TaskId { get; set; }

        public string? ActionCode { get; set; }

        public string? ActionName { get; set; }

        public string TargetTable { get; set; } = string.Empty;

        public Guid TargetId { get; set; }

        public string OpType { get; set; } = string.Empty;

        public Guid? OperatorId { get; set; }

        public string OperatorName { get; set; } = string.Empty;

        public string CreatedAt { get; set; } = string.Empty;

        public object? BeforeData { get; set; }

        public object? AfterData { get; set; }
    }
}
