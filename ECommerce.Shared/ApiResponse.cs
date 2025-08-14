namespace ECommerce.Shared;

public record ErrorItem(string Code, string Message, string? Field = null);

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string? CorrelationId { get; init; }
    public T? Data { get; init; }
    public List<ErrorItem>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string? cid = null) =>
        new() { Success = true, Data = data, CorrelationId = cid };

    public static ApiResponse<T> Fail(IEnumerable<ErrorItem> errors, string? cid = null) =>
        new() { Success = false, Errors = errors.ToList(), CorrelationId = cid };

    public static ApiResponse<T> Fail(string code, string message, string? field = null, string? cid = null) =>
        new() { Success = false, Errors = new() { new ErrorItem(code, message, field) }, CorrelationId = cid };
}
