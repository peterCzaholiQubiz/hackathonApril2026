namespace PortfolioThermometer.Api.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public ApiMeta? Meta { get; init; }

    public static ApiResponse<T> Ok(T data, ApiMeta? meta = null) =>
        new() { Success = true, Data = data, Meta = meta };

    public static ApiResponse<T> Fail(string error) =>
        new() { Success = false, Error = error };
}

public sealed class ApiMeta
{
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
