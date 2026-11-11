namespace CoreDesign.ExceptionHandling;

/// <summary>
/// Maps an exception type to an RFC 7807 <c>ProblemDetails</c> response. The included Roslyn
/// source generator collects every usage and emits a compile-time dispatch table consumed by
/// <see cref="CoreDesignExceptionHandler"/>.
///
/// Apply directly to an exception type you own:
/// <code>
/// [ProblemMapping(404, Title = "Resource not found")]
/// public sealed class EntityNotFoundException(string entity, Guid id)
///     : Exception($"{entity} '{id}' was not found.");
/// </code>
///
/// For an exception type you do not own (BCL, third-party), apply at the assembly level with
/// <see cref="ExceptionType"/> instead:
/// <code>
/// [assembly: ProblemMapping(408, ExceptionType = typeof(TaskCanceledException), Title = "Request timed out")]
/// </code>
///
/// By default a mapping also covers derived types (<see cref="MatchDerived"/>); the most specific
/// mapping in the inheritance chain wins. Two mappings for the exact same concrete type is a
/// compile-time error.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ProblemMappingAttribute(int statusCode) : Attribute
{
    /// <summary>The HTTP status code written to the response.</summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>The RFC 7807 <c>title</c>. Defaults to the standard reason phrase for <see cref="StatusCode"/> when omitted.</summary>
    public string? Title { get; init; }

    /// <summary>The RFC 7807 <c>type</c> URI. Omitted from the response when not set.</summary>
    public string? Type { get; init; }

    /// <summary>
    /// When <see langword="true"/> (the default), this mapping also matches subtypes of the target
    /// exception that do not carry their own, more specific <see cref="ProblemMappingAttribute"/>.
    /// </summary>
    public bool MatchDerived { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/> (the default), <see cref="Exception.Message"/> is written to the
    /// RFC 7807 <c>detail</c> field. Set to <see langword="false"/> for exceptions whose message
    /// should never reach a client.
    /// </summary>
    public bool IncludeMessage { get; init; } = true;

    /// <summary>
    /// The exception type this mapping targets. Required, and only meaningful, when the attribute
    /// is applied at the assembly level to map a type this project does not own.
    /// </summary>
    public Type? ExceptionType { get; init; }
}
