namespace cloud.api.Models
{
    public record ServiceResult(ResultReturn result, string? message = null); //NICE
    public record ServiceResult<T>(ResultReturn result, string? message, T? Value = default); //NICE
}
