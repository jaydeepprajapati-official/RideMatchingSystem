namespace RideMatching.Api.DTOs;
public class ApiErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? TraceId { get; set; }
}
