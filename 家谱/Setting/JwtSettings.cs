namespace 家谱.Setting
{
    public class JwtSettings
    {
        public string Secret { get; set; } = string.Empty;
        public int ExpirationMinutes { get; set; }
        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

    }
}
