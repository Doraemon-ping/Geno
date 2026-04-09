namespace 家谱.Common
{
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Unicode;

    /// <summary>
    /// Defines the <see cref="JsonDefaults" />
    /// 
    /// </summary>
    public static class JsonDefaults
    {
        /// <summary>
        /// Defines the Options
        /// </summary>
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
    }
}
