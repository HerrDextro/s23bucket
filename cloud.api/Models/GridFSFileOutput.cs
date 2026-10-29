namespace cloud.api.Models
{
    public class GridFSFileOutput
    {
        public GridFSFileOutput(string id, string name, string type, long size, DateTime uploadDate) //yes I know this is cursed as hell but I wasted enough time getting the google AI to tell me what the way is to populate the object in the foreach loop in GetDirectoryChildren()
        {
            Id = id;
            Name = name;
            Type = type;
            Size = size;
            UploadDate = uploadDate;

        }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public long Size { get; set; }
        public DateTime UploadDate { get; set; }
    }
}
