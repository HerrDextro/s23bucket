using cloud.api.Endpoints;
using cloud.api.Services;
using Microsoft.AspNetCore.Mvc;
namespace cloud.api.Dtos
{
    public class FileUploadDto
    {
        public IFormFile File { get; set; }

        [FromForm]
        public string DestinationPath {  get; set; }
        [FromForm]
        public UploadType Type { get; set; }

    }
}
