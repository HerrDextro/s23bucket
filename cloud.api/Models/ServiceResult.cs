namespace cloud.api.Models
{
    public record ServiceResult<T>(ResultReturn result, T? Value = default); //NICE
}
