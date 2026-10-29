using MongoDB.Driver.GridFS;

namespace cloud.api.Models
{
    public record GridFSFileResult
    (
        GridFSDownloadStream? Stream = null,
        string? Filename = null,
        string? ContentType = null
    );
}
