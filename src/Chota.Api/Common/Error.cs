namespace Chota.Api.Common;

public sealed record Error(string Code, string Description)
{
    public string Message => Description;
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "The specified value is null.");

    public static Error NotFound(string? description = null) =>
        new("Error.NotFound", description ?? "The requested resource was not found.");

    public static Error Validation(string description) =>
        new("Error.Validation", description);

    public static Error Conflict(string description) =>
        new("Error.Conflict", description);

    public static Error Failure(string description) =>
        new("Error.Failure", description);
}