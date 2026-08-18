using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Pharmacy.Api.Infrastructure;

/// <summary>
/// Turns every unhandled exception into an RFC 9457 <c>application/problem+json</c> document,
/// so that the Angular client sees one error shape for validation failures, domain rule
/// violations and unexpected faults alike.
/// </summary>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;

    public ProblemDetailsExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<ProblemDetailsExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        if (exception is DomainException domain)
        {
            _logger.LogInformation(
                "Domain rule refused {Method} {Path}: {Message}",
                httpContext.Request.Method, httpContext.Request.Path, domain.Message);

            problem = new ProblemDetails
            {
                Status = domain.StatusCode,
                Title = domain.Message,
                Detail = domain.Detail,
                Type = $"https://api.abcpharmacy.local/errors/{domain.Kind.ToString().ToLowerInvariant()}",
                Instance = httpContext.Request.Path
            };
        }
        else
        {
            _logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}.",
                httpContext.Request.Method, httpContext.Request.Path);

            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The failure has been logged. Please try again, or contact support with the correlation id.",
                Type = "https://api.abcpharmacy.local/errors/unexpected",
                Instance = httpContext.Request.Path
            };
        }

        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}
