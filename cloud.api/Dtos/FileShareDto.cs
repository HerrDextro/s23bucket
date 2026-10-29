using cloud.api.Services;
using cloud.api.Models;

namespace cloud.api.Dtos
{
    public class FileShareDto
    {
        public string FileId { get; set; }
        public Dictionary<string, FilePermissions> Permissions { get; set; }
    }
}
