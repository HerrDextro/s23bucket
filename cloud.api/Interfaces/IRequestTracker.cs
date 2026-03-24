namespace cloud.api.Interfaces
{
    public interface IRequestTracker
    {
        Dictionary<string, string>? GetStoredResourceRequest(string id);
    }
}
