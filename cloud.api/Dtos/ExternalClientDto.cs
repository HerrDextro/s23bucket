namespace cloud.api.Dtos
{
    public class ExternalClientDto
    {
        public string name { get; set; }
        public string url { get; set; } = string.Empty;
        public string api_key { get; set; } = string.Empty;
    }
}
