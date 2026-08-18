namespace Pharmacy.Api.Infrastructure;

public enum DomainErrorKind
{
    /// <summary>422 - the request was well formed but the domain refuses it.</summary>
    Rule = 0,

    /// <summary>404 - the referenced aggregate does not exist.</summary>
    NotFound = 1,

    /// <summary>409 - the request conflicts with the current state.</summary>
    Conflict = 2
}

/// <summary>
/// Thrown by the service layer when a business rule refuses a request. Translated into an
/// RFC 9457 problem+json document by <see cref="ProblemDetailsExceptionHandler"/>, so
/// controllers stay free of error-shaping code.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message, DomainErrorKind kind = DomainErrorKind.Rule, string? detail = null)
        : base(message)
    {
        Kind = kind;
        Detail = detail;
    }

    public DomainErrorKind Kind { get; }

    public string? Detail { get; }

    public int StatusCode => Kind switch
    {
        DomainErrorKind.NotFound => StatusCodes.Status404NotFound,
        DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    public static DomainException NotFound(string what, object id)
        => new($"{what} was not found.", DomainErrorKind.NotFound, $"No {what.ToLowerInvariant()} exists with id '{id}'.");
}
