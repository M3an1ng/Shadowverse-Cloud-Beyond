namespace ShadowverseBeyond.Server.Services;

public sealed class DeckOperationResult<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static DeckOperationResult<T> Ok(T data) => new DeckOperationResult<T>
    {
        Success = true,
        Data = data
    };

    public static DeckOperationResult<T> Fail(string error) => new DeckOperationResult<T>
    {
        Success = false,
        Error = error
    };
}
