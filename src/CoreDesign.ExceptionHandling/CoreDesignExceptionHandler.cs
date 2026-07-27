using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace CoreDesign.ExceptionHandling;

/// <summary>
/// The single catch point for unhandled exceptions escaping the request pipeline. Resolves the
/// exception to an RFC 7807 response via the registered <see cref="IProblemDetailsMapper"/> (the
/// generated mapping table, or <see cref="NullProblemDetailsMapper"/> when nothing is mapped) and
/// falls back to a generic 500. Register with <c>AddCoreDesignExceptionHandling()</c>.
/// </summary>
public sealed class CoreDesignExceptionHandler(
    IProblemDetailsMapper mapper,
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<CoreDesignExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var mapped = mapper.TryMap(exception, out var result)
            ? result
            : new ProblemMappingResult(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                environment.IsDevelopment() ? exception.ToString() : null,
                null);

        logger.LogError(exception, "Unhandled exception mapped to {StatusCode}", mapped.StatusCode);

        httpContext.Response.StatusCode = mapped.StatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = mapped.StatusCode,
                Title = mapped.Title ?? ReasonPhrases.GetReasonPhrase(mapped.StatusCode),
                Detail = mapped.Detail,
                Type = mapped.Type
            }
        });
    }
}
