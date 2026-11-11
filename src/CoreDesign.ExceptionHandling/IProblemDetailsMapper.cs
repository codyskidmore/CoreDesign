namespace CoreDesign.ExceptionHandling;

/// <summary>
/// Resolves an unhandled exception to an RFC 7807 <c>ProblemDetails</c> response.
/// The generator emits <c>GeneratedProblemDetailsMapper</c>, an implementation driven by
/// every <see cref="ProblemMappingAttribute"/> usage in the compilation.
/// </summary>
public interface IProblemDetailsMapper
{
    /// <summary>
    /// Attempts to resolve <paramref name="exception"/> to a mapped response.
    /// Returns <see langword="false"/> when no mapping applies, in which case
    /// <see cref="CoreDesignExceptionHandler"/> falls back to a generic 500 response.
    /// </summary>
    bool TryMap(Exception exception, out ProblemMappingResult result);
}
