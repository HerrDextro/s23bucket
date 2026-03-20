namespace cloud.api.Settings
{
    public class MongoSettings
    {
        public string ConnectionString { get; set; }
        public string IdentityDatabase { get; set; }
        public string MainDatabase { get; set; }

        public string ServiceDatabase { get; set; } 
    }
}
