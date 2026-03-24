namespace cloud.api.Settings;

public class JWTSettings
{
    public string SecretKey { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public int ExpiryInMinutes { get; set; }
    public int ExternalClientExpiryInMinutes { get; set; }
}

