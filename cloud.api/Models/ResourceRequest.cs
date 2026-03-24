namespace cloud.api.Models
{
    public class ResourceRequest
    {
        public DateTime Creation { get; set; }  
        public Dictionary<string, string> Permissions { get; set; }
    }
}

