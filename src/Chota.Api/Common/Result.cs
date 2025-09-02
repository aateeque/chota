namespace Chota.Api.Common;

public record Result<T>
{
    public T? Value { get; init; }
    public Error? Error { get; init; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    private Result(T value)
    {
        Value = value;
        Error = null;
    }

    private Result(Error error)
    {
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

public record Result
{
    public Error? Error { get; init; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    private Result() => Error = null;
    private Result(Error error) => Error = error;

    public static Result Success() => new();
    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}